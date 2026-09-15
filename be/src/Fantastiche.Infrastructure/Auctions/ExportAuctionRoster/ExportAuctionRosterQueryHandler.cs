using Dapper;
using Fantastiche.Infrastructure.Common;
using Fantastiche.Infrastructure.Common.Persistence;

namespace Fantastiche.Infrastructure.Auctions;

public sealed class ExportAuctionRosterQueryHandler(FantasticheDbContext db) : IRequestHandler<ExportAuctionRosterQuery, AuctionRosterExportView>
{
    public async Task<AuctionRosterExportView> HandleAsync(ExportAuctionRosterQuery request, CancellationToken ct)
    {
        await using var read = await AuctionReadSession.OpenAsync(db, request.Context, request.SessionId, ct);
        // Una lettura sotto il lock condiviso dell'asta include l'intera stagione, anche su più sessioni.
        var rows = (await read.Connection.QueryAsync<FantacalcioRosterRow>(new CommandDefinition("""
            SELECT r.TeamId, team.Name AS TeamName, player.Source, player.ExternalId, r.Price
            FROM RosterEntries r
            INNER JOIN Teams team ON team.Id = r.TeamId
              AND team.LeagueId = r.LeagueId AND team.LeagueSeasonId = r.LeagueSeasonId
            INNER JOIN Players player ON player.Id = r.PlayerId
            WHERE r.LeagueId = @LeagueId AND r.LeagueSeasonId = @LeagueSeasonId
            ORDER BY team.Name, team.Id,
              CASE r.Role WHEN N'P' THEN 0 WHEN N'D' THEN 1 WHEN N'C' THEN 2 ELSE 3 END,
              r.AcquiredAt, r.PlayerId;
            """, new { read.Session.LeagueId, read.Session.LeagueSeasonId }, read.Transaction, cancellationToken: ct))).AsList();
        await read.CommitAsync(ct);
        return new($"fantastiche-rosters-{read.Session.LeagueSeasonId:N}.csv", FantacalcioRosterCsv.Write(rows));
    }
}
