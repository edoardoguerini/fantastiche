using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;

namespace Fantastiche.Infrastructure.Auctions;

public sealed partial class AuctionEngine
{
    public async Task<bool> CloseAsync(Guid playerAuctionId, CancellationToken ct)
    {
        await using var c = Connection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var original = await c.QuerySingleOrDefaultAsync<PlayerAuction>(Sql($"SELECT {PlayerColumns} FROM PlayerAuctions WHERE Id = @Id", new { Id = playerAuctionId }, tx, ct));
        if (original is null) return false;
        await AuctionSqlLock.AcquireAsync(c, tx, original.LeagueSeasonId, true, ct);
        var auction = await c.QuerySingleAsync<PlayerAuction>(Sql($"SELECT {PlayerColumns} FROM PlayerAuctions WHERE Id = @Id AND SessionId = @SessionId AND LeagueSeasonId = @LeagueSeasonId AND LeagueId = @LeagueId", original, tx, ct));
        var now = await c.ExecuteScalarAsync<DateTimeOffset>(Sql(SqlNow, null, tx, ct));
        if (auction.Status != PlayerAuctionStatus.Open || now < auction.Deadline) return false;
        var session = await c.QuerySingleAsync<AuctionSession>(Sql($"SELECT {SessionColumns} FROM AuctionSessions WHERE Id = @SessionId AND LeagueSeasonId = @LeagueSeasonId AND LeagueId = @LeagueId", auction, tx, ct));
        if (session.Status != AuctionSessionStatus.Active) return false;
        var debited = await c.ExecuteAsync(Sql("""
            UPDATE Teams SET Budget = Budget - @CurrentAmount
            WHERE Id = @WinningTeamId AND LeagueSeasonId = @LeagueSeasonId AND LeagueId = @LeagueId AND Budget >= @CurrentAmount
            """, auction, tx, ct));
        if (debited != 1) throw new InvalidOperationException($"Budget incoerente alla chiusura dell’asta {auction.Id:D}.");
        await c.ExecuteAsync(Sql("""
            INSERT INTO RosterEntries (LeagueSeasonId, PlayerId, LeagueId, TeamId, PlayerAuctionId, Role, Price, AcquiredAt)
            VALUES (@LeagueSeasonId, @PlayerId, @LeagueId, @TeamId, @PlayerAuctionId, @Role, @Price, @AcquiredAt);
            INSERT INTO BudgetMovements (Id, LeagueSeasonId, LeagueId, TeamId, PlayerAuctionId, Amount, CreatedAt)
            VALUES (@MovementId, @LeagueSeasonId, @LeagueId, @TeamId, @PlayerAuctionId, @Amount, @AcquiredAt);
            UPDATE PlayerAuctions SET Status = 1, ClosedAt = @AcquiredAt
            WHERE Id = @PlayerAuctionId AND SessionId = @SessionId AND LeagueSeasonId = @LeagueSeasonId AND LeagueId = @LeagueId AND Status = 0;
            """, new
        {
            auction.LeagueSeasonId,
            auction.PlayerId,
            auction.LeagueId,
            TeamId = auction.WinningTeamId,
            PlayerAuctionId = auction.Id,
            auction.SessionId,
            auction.Role,
            Price = auction.CurrentAmount,
            AcquiredAt = now,
            MovementId = Guid.CreateVersion7(),
            Amount = -auction.CurrentAmount
        }, tx, ct));
        await AdvanceAsync(c, tx, session, ct);
        await SaveSessionAsync(c, tx, session, ct);
        await tx.CommitAsync(ct);
        return true;
    }

    private sealed class DueAuction
    {
        public Guid Id { get; set; }
        public DateTimeOffset Deadline { get; set; }
    }

    public async Task<int> CloseExpiredAsync(CancellationToken ct)
    {
        await using var c = Connection();
        await c.OpenAsync(ct);
        var cutoff = await c.ExecuteScalarAsync<DateTimeOffset>(new CommandDefinition(SqlNow, cancellationToken: ct));
        DueAuction? cursor = null;
        var closed = 0;
        while (closed < 50)
        {
            ct.ThrowIfCancellationRequested();
            var due = (await c.QueryAsync<DueAuction>(new CommandDefinition("""
                SELECT TOP (@BatchSize) Id, Deadline FROM PlayerAuctions
                WHERE Status = 0 AND Deadline <= @Cutoff
                    AND (@LastDeadline IS NULL OR Deadline > @LastDeadline OR (Deadline = @LastDeadline AND Id > @LastId))
                ORDER BY Deadline, Id
                """, new { BatchSize = 50 - closed, Cutoff = cutoff, LastDeadline = cursor?.Deadline, LastId = cursor?.Id }, cancellationToken: ct))).ToArray();
            if (due.Length == 0) break;
            foreach (var auction in due)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (await CloseAsync(auction.Id, ct)) closed++;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception error)
                {
                    ct.ThrowIfCancellationRequested();
                    logger.LogError(error, "Chiusura dell’asta {PlayerAuctionId} non riuscita; il lotto continua.", auction.Id);
                }
            }
            // Il cursore avanza anche sugli errori: le stagioni successive non rimangono bloccate.
            cursor = due[^1];
        }
        return closed;
    }
}
