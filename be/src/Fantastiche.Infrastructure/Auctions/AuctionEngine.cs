using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using Fantastiche.Core.Auth;
using Fantastiche.Core.Exceptions;
using Fantastiche.Infrastructure.Common;
using Fantastiche.Infrastructure.Common.Persistence;
using Fantastiche.Infrastructure.Leagues;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fantastiche.Infrastructure.Auctions;

public sealed partial class AuctionEngine(FantasticheDbContext db, IRequestPublisher publisher, ILogger<AuctionEngine> logger)
{
    private const string SqlNow = "SELECT TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')";
    private const string SessionColumns = "Id, LeagueId, LeagueSeasonId, ListVersionId, Status, CurrentPosition, Version, CreatedAt, CreatedByUserId";
    private const string PlayerColumns = "Id, SessionId, LeagueSeasonId, LeagueId, ListVersionId, PlayerId, Number, CallerTeamId, WinningTeamId, Role, DurationSeconds, IncrementOptionsJson, CurrentAmount, BidSequence, Deadline, Status, StartedAt, ClosedAt";

    private SqlConnection Connection() => new(db.Database.GetConnectionString());
    private static CommandDefinition Sql(string sql, object? args, DbTransaction tx, CancellationToken ct) => new(sql, args, tx, cancellationToken: ct);
    private static DomainException Error(string code, string message, int status = 409) => new(code, message, status);
    private static void RequireId(Guid id)
    {
        if (id == Guid.Empty) throw Error("auction.invalid_request", "Identificativo obbligatorio.", 400);
    }

    private static void RequireUser(RequestContext context)
    {
        if (context.UserId is null || context.UserId == Guid.Empty) throw Error("auth.required", "Accesso richiesto.", 401);
    }

    private static async Task AuthorizeAsync(DbConnection c, DbTransaction tx, RequestContext context, Guid leagueId, bool organizer, CancellationToken ct)
    {
        RequireUser(context);
        if (context.IsSuperAdmin)
        {
            var isSuperAdmin = await c.ExecuteScalarAsync<int>(Sql("""
                SELECT COUNT(*) FROM AspNetUserRoles userRole
                INNER JOIN AspNetRoles role ON role.Id = userRole.RoleId
                WHERE userRole.UserId = @UserId AND role.NormalizedName = N'SUPERADMIN'
                """, new { context.UserId }, tx, ct));
            if (isSuperAdmin > 0) return;
        }
        var permitted = await c.ExecuteScalarAsync<int>(Sql("""
            SELECT COUNT(*) FROM LeagueMembers
            WHERE LeagueId = @LeagueId AND UserId = @UserId AND Status = 1 AND (@Organizer = 0 OR IsOrganizer = 1)
            """, new { LeagueId = leagueId, context.UserId, Organizer = organizer }, tx, ct));
        if (permitted == 0) throw Error("auth.forbidden", "Operazione non consentita.", 403);
    }

    private static Task<AuctionSession?> ReadSessionAsync(DbConnection c, DbTransaction tx, Guid id, CancellationToken ct) =>
        c.QuerySingleOrDefaultAsync<AuctionSession>(Sql($"SELECT {SessionColumns} FROM AuctionSessions WHERE Id = @Id", new { Id = id }, tx, ct));

    private static Task<AuctionSession> ReadLockedSessionAsync(DbConnection c, DbTransaction tx, AuctionSession original, CancellationToken ct) =>
        c.QuerySingleAsync<AuctionSession>(Sql($"SELECT {SessionColumns} FROM AuctionSessions WHERE Id = @Id AND LeagueSeasonId = @LeagueSeasonId AND LeagueId = @LeagueId", original, tx, ct));

    private static Task<LeagueSeason> ReadRulesAsync(DbConnection c, DbTransaction tx, AuctionSession s, CancellationToken ct) =>
        c.QuerySingleAsync<LeagueSeason>(Sql("""
            SELECT Id, LeagueId, Name, ListVersionId, Budget, Goalkeepers, Defenders, Midfielders, Forwards
            FROM LeagueSeasons WHERE Id = @LeagueSeasonId AND LeagueId = @LeagueId
            """, s, tx, ct));

    private static Task<PlayerAuction?> ReadOpenAsync(DbConnection c, DbTransaction tx, AuctionSession s, CancellationToken ct) =>
        c.QuerySingleOrDefaultAsync<PlayerAuction>(Sql($"SELECT {PlayerColumns} FROM PlayerAuctions WHERE SessionId = @Id AND LeagueSeasonId = @LeagueSeasonId AND LeagueId = @LeagueId AND Status = 0", s, tx, ct));

    private static async Task<Guid> ParticipantAsync(DbConnection c, DbTransaction tx, AuctionSession s, RequestContext context, CancellationToken ct)
    {
        var team = await c.QuerySingleOrDefaultAsync<Guid?>(Sql("""
            SELECT tm.TeamId FROM TeamMembers tm
            INNER JOIN LeagueMembers lm ON lm.LeagueId = tm.LeagueId AND lm.UserId = tm.UserId AND lm.Status = 1
            INNER JOIN CallOrderEntries co ON co.TeamId = tm.TeamId AND co.LeagueSeasonId = tm.LeagueSeasonId AND co.LeagueId = tm.LeagueId
            WHERE co.SessionId = @Id AND tm.LeagueSeasonId = @LeagueSeasonId AND tm.LeagueId = @LeagueId AND tm.UserId = @UserId
            """, new { s.Id, s.LeagueSeasonId, s.LeagueId, context.UserId }, tx, ct));
        return team ?? throw Error("auction.not_participant", "Non partecipi a questa sessione con una squadra attiva.", 403);
    }

    private sealed class Capacity
    {
        public int Budget { get; set; }
        public int Total { get; set; }
        public int InRole { get; set; }
    }

    private static async Task ValidateCapacityAsync(DbConnection c, DbTransaction tx, AuctionSession s, Guid teamId, string role, int amount, CancellationToken ct)
    {
        var rules = await ReadRulesAsync(c, tx, s, ct);
        var capacity = await c.QuerySingleAsync<Capacity>(Sql("""
            SELECT t.Budget, COUNT(r.PlayerId) AS Total, COALESCE(SUM(CASE WHEN r.Role = @Role THEN 1 ELSE 0 END), 0) AS InRole
            FROM Teams t LEFT JOIN RosterEntries r ON r.TeamId = t.Id AND r.LeagueSeasonId = t.LeagueSeasonId AND r.LeagueId = t.LeagueId
            WHERE t.Id = @TeamId AND t.LeagueSeasonId = @LeagueSeasonId AND t.LeagueId = @LeagueId
            GROUP BY t.Budget
            """, new { TeamId = teamId, s.LeagueSeasonId, s.LeagueId, Role = role }, tx, ct));
        var total = rules.Goalkeepers + rules.Defenders + rules.Midfielders + rules.Forwards;
        var roleLimit = role switch { "P" => rules.Goalkeepers, "D" => rules.Defenders, "C" => rules.Midfielders, "A" => rules.Forwards, _ => 0 };
        if (capacity.Total >= total) throw Error("auction.roster_full", "La rosa è completa.");
        if (capacity.InRole >= roleLimit) throw Error("auction.role_full", "I posti per questo ruolo sono esauriti.");
        if ((long)capacity.Budget - amount < total - capacity.Total - 1)
            throw Error("auction.insufficient_budget", "Budget insufficiente, considerando i posti ancora da completare.");
    }

    private static async Task SaveSessionAsync(DbConnection c, DbTransaction tx, AuctionSession s, CancellationToken ct)
    {
        s.Version++;
        await c.ExecuteAsync(Sql("""
            UPDATE AuctionSessions SET Version = @Version, Status = @Status, CurrentPosition = @CurrentPosition
            WHERE Id = @Id AND LeagueSeasonId = @LeagueSeasonId AND LeagueId = @LeagueId
            """, s, tx, ct));
    }

    private async Task<AuctionCommandResult> ExecuteCommandAsync(RequestContext context, Guid sessionId, Guid requestId,
        string commandType, object payload, Guid? auctionId,
        Func<DbConnection, DbTransaction, AuctionSession, DateTimeOffset, CancellationToken, Task<Guid?>> execute, CancellationToken ct)
    {
        RequireUser(context);
        RequireId(sessionId);
        RequireId(requestId);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonSerializerOptions.Web))));
        await using var c = Connection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var original = await ReadSessionAsync(c, tx, sessionId, ct) ?? throw Error("resource.not_found", "Sessione non disponibile.", 404);
        await AuctionSqlLock.AcquireAsync(c, tx, original.LeagueSeasonId, true, ct);
        var s = await ReadLockedSessionAsync(c, tx, original, ct);
        await AuthorizeAsync(c, tx, context, s.LeagueId, false, ct);
        var now = await c.ExecuteScalarAsync<DateTimeOffset>(Sql(SqlNow, null, tx, ct));
        var receipt = await c.QuerySingleOrDefaultAsync<CommandReceipt>(Sql("""
            SELECT SessionId, UserId, RequestId, CommandType, PayloadHash, ResultJson, CreatedAt FROM CommandReceipts
            WHERE SessionId = @SessionId AND UserId = @UserId AND RequestId = @RequestId
            """, new { SessionId = sessionId, context.UserId, RequestId = requestId }, tx, ct));
        if (receipt is not null)
        {
            var replay = receipt.CommandType == commandType && receipt.PayloadHash == hash
                ? JsonSerializer.Deserialize<AuctionCommandResult>(receipt.ResultJson, JsonSerializerOptions.Web)!
                : new AuctionCommandResult(requestId, sessionId, auctionId, s.Version, now, false, 409, "auction.request_conflict", "RequestId già usato per un altro comando o payload.");
            await tx.CommitAsync(ct);
            return replay;
        }

        AuctionCommandResult result;
        try
        {
            auctionId = await execute(c, tx, s, now, ct);
            result = new(requestId, sessionId, auctionId, s.Version, now, true);
        }
        catch (DomainException error)
        {
            // Ogni invariante viene verificata prima delle scritture del comando.
            result = new(requestId, sessionId, auctionId, s.Version, now, false, error.StatusCode, error.Code, error.Message);
        }
        await c.ExecuteAsync(Sql("""
            INSERT INTO CommandReceipts (SessionId, UserId, RequestId, CommandType, PayloadHash, ResultJson, CreatedAt)
            VALUES (@SessionId, @UserId, @RequestId, @CommandType, @PayloadHash, @ResultJson, @CreatedAt)
            """, new
        {
            SessionId = sessionId,
            context.UserId,
            RequestId = requestId,
            CommandType = commandType,
            PayloadHash = hash,
            ResultJson = JsonSerializer.Serialize(result, JsonSerializerOptions.Web),
            CreatedAt = now
        }, tx, ct));
        await tx.CommitAsync(ct);
        return result;
    }
}
