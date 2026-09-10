using System.Data;
using System.Data.Common;
using System.Text.Json;
using Dapper;
using Fantastiche.Core.Auth;
using Fantastiche.Core.Exceptions;
using Fantastiche.Infrastructure.Catalog;
using Fantastiche.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fantastiche.Infrastructure.Auctions;

internal sealed partial class AuctionReadSession : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly DbConnection connection;
    private readonly DbTransaction transaction;
    private readonly RequestContext context;

    private AuctionReadSession(DbConnection connection, DbTransaction transaction, AuctionSessionIdentity session, RequestContext context)
    {
        this.connection = connection;
        this.transaction = transaction;
        Session = session;
        this.context = context;
    }

    internal AuctionSessionIdentity Session { get; }

    internal static async Task<AuctionReadSession> OpenAsync(
        FantasticheDbContext db,
        RequestContext context,
        Guid sessionId,
        CancellationToken ct)
    {
        RequireAuthenticated(context);
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(ct);
        var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            var session = await connection.QuerySingleOrDefaultAsync<AuctionSessionIdentity>(new CommandDefinition("""
                SELECT Id, LeagueId, LeagueSeasonId
                FROM AuctionSessions
                WHERE Id = @sessionId;
                """, new { sessionId }, transaction, cancellationToken: ct)) ?? throw NotFound();
            await RequireAccessAsync(connection, transaction, context, session.LeagueId, session.LeagueSeasonId, ct);
            await AuctionSqlLock.AcquireAsync(connection, transaction, session.LeagueSeasonId, false, ct);
            await RequireAccessAsync(connection, transaction, context, session.LeagueId, session.LeagueSeasonId, ct);
            return new AuctionReadSession(connection, transaction, session, context);
        }
        catch
        {
            await transaction.DisposeAsync();
            await connection.CloseAsync();
            throw;
        }
    }

    internal static async Task<AuctionReadSession?> OpenActiveAsync(
        FantasticheDbContext db,
        RequestContext context,
        Guid leagueId,
        Guid leagueSeasonId,
        CancellationToken ct)
    {
        RequireAuthenticated(context);
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(ct);
        var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            await RequireAccessAsync(connection, transaction, context, leagueId, leagueSeasonId, ct);
            await AuctionSqlLock.AcquireAsync(connection, transaction, leagueSeasonId, false, ct);
            await RequireAccessAsync(connection, transaction, context, leagueId, leagueSeasonId, ct);
            var session = await connection.QuerySingleOrDefaultAsync<AuctionSessionIdentity>(new CommandDefinition("""
                SELECT Id, LeagueId, LeagueSeasonId
                FROM AuctionSessions
                WHERE LeagueId = @leagueId AND LeagueSeasonId = @leagueSeasonId AND Status < 2;
                """, new { leagueId, leagueSeasonId }, transaction, cancellationToken: ct));
            if (session is not null) return new AuctionReadSession(connection, transaction, session, context);
            await transaction.CommitAsync(ct);
            await transaction.DisposeAsync();
            await connection.CloseAsync();
            return null;
        }
        catch
        {
            await transaction.DisposeAsync();
            await connection.CloseAsync();
            throw;
        }
    }

    internal async Task<AuctionSessionView> ReadStateAsync(PlayerPhotoStorage photos, ClubLogoStorage logos, CancellationToken ct)
    {
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition($"""
            SELECT s.Id, s.LeagueId, s.LeagueSeasonId, s.ListVersionId, s.Status, s.Version,
                   CASE WHEN s.Status = 2 THEN NULL ELSE currentEntry.TeamId END AS CurrentTeamId,
                   TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00') AS ServerTime
            FROM AuctionSessions s
            LEFT JOIN CallOrderEntries currentEntry
              ON currentEntry.SessionId = s.Id AND currentEntry.Position = s.CurrentPosition
            WHERE s.Id = @sessionId;

            SELECT TeamId
            FROM CallOrderEntries
            WHERE SessionId = @sessionId
            ORDER BY Position;

            SELECT TOP (1) pa.Id, pa.PlayerId, entry.Name, pa.Role, entry.ClubName,
                   entry.BirthDate, entry.Nationality, entry.PreferredFoot,
                   entry.CurrentQuotation, entry.InitialQuotation, entry.Fvm,
                   pa.CallerTeamId, pa.WinningTeamId, pa.CurrentAmount, pa.DurationSeconds,
                   pa.IncrementOptionsJson, pa.Deadline, pa.Status, pa.StartedAt, pa.ClosedAt,
                   {PlayerPhotoStorage.SqlProjection}, {ClubLogoStorage.SqlProjection}
            FROM PlayerAuctions pa
            INNER JOIN ListEntries entry
              ON entry.ListVersionId = pa.ListVersionId AND entry.PlayerId = pa.PlayerId
            INNER JOIN Players player ON player.Id = pa.PlayerId
            LEFT JOIN PlayerMedia media ON media.Source = player.Source AND media.ExternalId = player.ExternalId
            INNER JOIN Clubs club ON club.Id = entry.ClubId
            LEFT JOIN ClubMedia clubMedia ON clubMedia.Source = club.Source AND clubMedia.NormalizedClubName = club.NormalizedName
            WHERE pa.SessionId = @sessionId
            ORDER BY pa.Number DESC;

            SELECT team.Id, team.Name, team.Budget,
                   SUM(CASE WHEN roster.Role = N'P' THEN 1 ELSE 0 END) AS Goalkeepers,
                   SUM(CASE WHEN roster.Role = N'D' THEN 1 ELSE 0 END) AS Defenders,
                   SUM(CASE WHEN roster.Role = N'C' THEN 1 ELSE 0 END) AS Midfielders,
                   SUM(CASE WHEN roster.Role = N'A' THEN 1 ELSE 0 END) AS Forwards
            FROM CallOrderEntries callOrder
            INNER JOIN Teams team
              ON team.Id = callOrder.TeamId
             AND team.LeagueSeasonId = callOrder.LeagueSeasonId
             AND team.LeagueId = callOrder.LeagueId
            LEFT JOIN RosterEntries roster
              ON roster.LeagueSeasonId = @leagueSeasonId
             AND roster.LeagueId = @leagueId
             AND roster.TeamId = team.Id
            WHERE callOrder.SessionId = @sessionId
            GROUP BY callOrder.Position, team.Id, team.Name, team.Budget
            ORDER BY callOrder.Position;
            """, new
        {
            sessionId = Session.Id,
            leagueSeasonId = Session.LeagueSeasonId,
            PhotoBaseUrl = photos.PublicBaseUrl,
            ClubLogoBaseUrl = logos.PublicBaseUrl,
            leagueId = Session.LeagueId
        }, transaction, cancellationToken: ct));

        var header = await grid.ReadSingleOrDefaultAsync<AuctionStateHeader>() ?? throw NotFound();
        var teamOrder = (await grid.ReadAsync<Guid>()).AsList();
        var auctionRow = await grid.ReadSingleOrDefaultAsync<AuctionPlayerRow>();
        var teams = (await grid.ReadAsync<AuctionTeamView>()).AsList();
        var currentAuction = auctionRow is null ? null : new AuctionPlayerView(
            auctionRow.Id,
            auctionRow.PlayerId,
            auctionRow.Name,
            auctionRow.Role,
            auctionRow.ClubName,
            auctionRow.CallerTeamId,
            auctionRow.WinningTeamId,
            auctionRow.CurrentAmount,
            auctionRow.DurationSeconds,
            JsonSerializer.Deserialize<int[]>(auctionRow.IncrementOptionsJson, JsonOptions) ?? [],
            auctionRow.Deadline,
            PlayerStatus(auctionRow.Status),
            auctionRow.StartedAt,
            auctionRow.ClosedAt,
            auctionRow.PhotoUrl,
            auctionRow.ClubLogoUrl,
            auctionRow.BirthDate,
            auctionRow.Nationality,
            auctionRow.PreferredFoot,
            auctionRow.CurrentQuotation,
            auctionRow.InitialQuotation,
            auctionRow.Fvm);

        var currentBomb = await ReadBombStateAsync(photos, logos, ct);
        var progress = await AuctionTurnProgress.ReadAsync(connection, transaction, Session.Id, Session.LeagueId, Session.LeagueSeasonId, ct);
        var position = progress.FindPosition(teamOrder.IndexOf(header.CurrentTeamId ?? Guid.Empty), false);
        return new AuctionSessionView(
            header.Id,
            header.LeagueId,
            header.LeagueSeasonId,
            header.ListVersionId,
            SessionStatus(header.Status),
            header.Version,
            header.Status == 2 ? null : currentAuction?.Status == "Open" ? header.CurrentTeamId : position is { } index ? teamOrder[index] : null,
            teamOrder,
            currentAuction,
            teams,
            header.ServerTime,
            header.Status == 2 ? null : currentAuction?.Status == "Open" ? currentAuction.Role : progress.Role,
            currentBomb);
    }

    internal DbConnection Connection => connection;
    internal DbTransaction Transaction => transaction;

    internal Task CommitAsync(CancellationToken ct) => transaction.CommitAsync(ct);

    public async ValueTask DisposeAsync()
    {
        await transaction.DisposeAsync();
        await connection.CloseAsync();
    }

    internal static void ValidatePage(int page, int pageSize)
    {
        if (page is < 1 or > 10_000 || pageSize is < 1 or > 100)
            throw new DomainException("auction.invalid_query", "Paginazione non valida.");
    }

    internal static DomainException NotFound() =>
        new("resource.not_found", "Risorsa non disponibile.", 404);

    internal static async Task RequireAccessAsync(
        DbConnection connection,
        DbTransaction transaction,
        RequestContext context,
        Guid leagueId,
        Guid leagueSeasonId,
        CancellationToken ct)
    {
        var seasonExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*)
            FROM LeagueSeasons
            WHERE Id = @leagueSeasonId AND LeagueId = @leagueId;
            """, new { leagueId, leagueSeasonId }, transaction, cancellationToken: ct));
        if (seasonExists == 0) throw NotFound();
        if (context.IsSuperAdmin)
        {
            var isSuperAdmin = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                SELECT COUNT(*)
                FROM AspNetUserRoles userRole
                INNER JOIN AspNetRoles role ON role.Id = userRole.RoleId
                WHERE userRole.UserId = @userId AND role.NormalizedName = N'SUPERADMIN';
                """, new { userId = context.UserId }, transaction, cancellationToken: ct));
            if (isSuperAdmin > 0) return;
        }
        var activeMember = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*)
            FROM LeagueMembers
            WHERE LeagueId = @leagueId AND UserId = @userId AND Status = 1;
            """, new { leagueId, userId = context.UserId }, transaction, cancellationToken: ct));
        if (activeMember == 0)
            throw new DomainException("auth.forbidden", "Operazione non consentita.", 403);
    }

    internal static void RequireAuthenticated(RequestContext context)
    {
        if (context.UserId is null || context.UserId == Guid.Empty)
            throw new DomainException("auth.required", "Accesso richiesto.", 401);
    }

    private static string SessionStatus(int status) => status switch
    {
        0 => "Active",
        1 => "Paused",
        2 => "Completed",
        _ => throw new InvalidOperationException("Stato sessione d'asta non valido.")
    };

    private static string PlayerStatus(int status) => status switch
    {
        0 => "Open",
        1 => "Closed",
        _ => throw new InvalidOperationException("Stato asta giocatore non valido.")
    };

    internal sealed class AuctionSessionIdentity
    {
        public Guid Id { get; init; }
        public Guid LeagueId { get; init; }
        public Guid LeagueSeasonId { get; init; }
    }

    private sealed class AuctionStateHeader
    {
        public Guid Id { get; init; }
        public Guid LeagueId { get; init; }
        public Guid LeagueSeasonId { get; init; }
        public Guid ListVersionId { get; init; }
        public int Status { get; init; }
        public long Version { get; init; }
        public Guid? CurrentTeamId { get; init; }
        public DateTimeOffset ServerTime { get; init; }
    }

    private sealed class AuctionPlayerRow
    {
        public Guid Id { get; init; }
        public Guid PlayerId { get; init; }
        public string Name { get; init; } = "";
        public string Role { get; init; } = "";
        public string ClubName { get; init; } = "";
        public Guid CallerTeamId { get; init; }
        public Guid WinningTeamId { get; init; }
        public int CurrentAmount { get; init; }
        public int DurationSeconds { get; init; }
        public string IncrementOptionsJson { get; init; } = "";
        public DateTimeOffset Deadline { get; init; }
        public int Status { get; init; }
        public DateTimeOffset StartedAt { get; init; }
        public DateTimeOffset? ClosedAt { get; init; }
        public string? PhotoUrl { get; init; }
        public string? ClubLogoUrl { get; init; }
        public DateTime? BirthDate { get; init; }
        public string? Nationality { get; init; }
        public string? PreferredFoot { get; init; }
        public int? CurrentQuotation { get; init; }
        public int? InitialQuotation { get; init; }
        public int? Fvm { get; init; }
    }
}
