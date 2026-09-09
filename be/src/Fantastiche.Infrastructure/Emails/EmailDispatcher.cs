using System.Data;
using System.Data.Common;
using Dapper;
using Fantastiche.Core.Email;
using Fantastiche.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
namespace Fantastiche.Infrastructure.Emails;

public sealed class EmailDispatcher(FantasticheDbContext db, IEmailSender sender, EmailPayloadProtector protector,
 TimeProvider clock, ILogger<EmailDispatcher> logger)
{
    public async Task<bool> DispatchOneAsync(CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        var now = clock.GetUtcNow(); var lease = Guid.CreateVersion7();
        // Pulizia delle credenziali di inviti non più spedibili, inclusi lease abbandonati.
        await connection.ExecuteAsync(new CommandDefinition("""
   UPDATE e SET Status = 4, ProtectedPayload = '', LeaseId = NULL, LeaseExpiresAt = NULL
   FROM EmailMessages e JOIN LeagueInvitations i ON i.Id = e.InvitationId
   WHERE e.Status IN (0,1) AND (i.RevokedAt IS NOT NULL OR i.AcceptedAt IS NOT NULL OR i.ExpiresAt <= @now);
   """, new { now }, cancellationToken: ct));
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(ct);
        await using var claim = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var message = await connection.QuerySingleOrDefaultAsync<EmailMessage>(new CommandDefinition("""
   ;WITH candidate AS (
    SELECT TOP (1) e.* FROM EmailMessages e WITH (UPDLOCK, READPAST, READCOMMITTEDLOCK)
    WHERE (e.Status = 0 AND e.NextAttemptAt <= @now OR e.Status = 1 AND e.LeaseExpiresAt <= @now)
     AND e.Attempts < 5
     AND EXISTS (SELECT 1 FROM LeagueInvitations i WHERE i.Id = e.InvitationId
      AND i.RevokedAt IS NULL AND i.AcceptedAt IS NULL AND i.ExpiresAt > @now)
    ORDER BY e.NextAttemptAt, e.Id
   )
   UPDATE candidate SET Status = 1, Attempts = Attempts + 1, LeaseId = @lease, LeaseExpiresAt = @expires
   OUTPUT inserted.Id, inserted.InvitationId, inserted.ToAddress, inserted.ProtectedPayload,
    inserted.Status, inserted.Attempts, inserted.CreatedAt, inserted.NextAttemptAt,
    inserted.LeaseId, inserted.LeaseExpiresAt, inserted.ProviderMessageId, inserted.LastErrorCode;
   """, new { now, lease, expires = now.AddMinutes(2) }, transaction: claim, cancellationToken: ct));
        await claim.CommitAsync(ct);
        if (message is null)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
    UPDATE EmailMessages SET Status = 3, ProtectedPayload = '', LeaseId = NULL, LeaseExpiresAt = NULL, LastErrorCode = 'email.attempts_exhausted'
    WHERE Status = 1 AND Attempts >= 5 AND LeaseExpiresAt <= @now;
    """, new { now }, cancellationToken: ct));
            return false;
        }
        try
        {
            // Ricontrollo prima della chiamata esterna: una revoca successiva resta una race inevitabile senza transazione sul provider.
            var valid = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
    SELECT COUNT(*) FROM EmailMessages e JOIN LeagueInvitations i ON i.Id=e.InvitationId
    WHERE e.Id=@id AND e.LeaseId=@lease AND e.Status=1 AND i.RevokedAt IS NULL AND i.AcceptedAt IS NULL AND i.ExpiresAt>@now;
    """, new { id = message.Id, lease, now = clock.GetUtcNow() }, cancellationToken: ct));
            if (valid == 0) return true;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            var result = await sender.SendAsync(protector.Unprotect(message.ProtectedPayload), timeout.Token);
            await connection.ExecuteAsync(new CommandDefinition("""
    UPDATE EmailMessages SET Status = 2, ProviderMessageId = @provider, ProtectedPayload = '', LeaseId = NULL, LeaseExpiresAt = NULL, LastErrorCode = NULL
    WHERE Id = @id AND LeaseId = @lease AND Status = 1;
    """, new { id = message.Id, lease, provider = result.ProviderMessageId }, cancellationToken: ct));
            logger.LogInformation("Email {EmailMessageId} accettata dal mittente configurato", message.Id);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        // Un errore del database lascia il lease recuperabile: non è un fallimento del provider.
        catch (Exception ex) when (ex is not DbException)
        {
            var transient = ex is HttpRequestException or OperationCanceledException || ex is EmailDeliveryException { Transient: true };
            var retry = transient && message.Attempts < 5;
            await connection.ExecuteAsync(new CommandDefinition("""
    UPDATE EmailMessages SET Status = @status, NextAttemptAt = @next,
     ProtectedPayload = CASE WHEN @status = 3 THEN '' ELSE ProtectedPayload END,
     LeaseId = NULL, LeaseExpiresAt = NULL, LastErrorCode = @error
    WHERE Id = @id AND LeaseId = @lease AND Status = 1;
    """, new
            {
                id = message.Id,
                lease,
                status = retry ? 0 : 3,
                next = clock.GetUtcNow().AddSeconds(Math.Pow(2, message.Attempts) * 30),
                error = transient ? "email.delivery_failed" : "email.permanent_failure"
            }, cancellationToken: ct));
            logger.LogWarning("Invio email {EmailMessageId} fallito; retry {Retry}", message.Id, retry);
        }
        return true;
    }
}
