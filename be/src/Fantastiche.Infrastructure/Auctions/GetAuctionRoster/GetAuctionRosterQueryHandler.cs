using Dapper;
using Fantastiche.Infrastructure.Common;
using Fantastiche.Infrastructure.Common.Persistence;

namespace Fantastiche.Infrastructure.Auctions;

public sealed class GetAuctionRosterQueryHandler(FantasticheDbContext db) : IRequestHandler<GetAuctionRosterQuery, AuctionPage<AuctionRosterView>>
{
    public async Task<AuctionPage<AuctionRosterView>> HandleAsync(GetAuctionRosterQuery request, CancellationToken ct)
    {
        AuctionReadSession.ValidatePage(request.Page, request.PageSize);
        await using var read = await AuctionReadSession.OpenAsync(db, request.Context, request.SessionId, ct);
        var parameters = new
        {
            request.TeamId,
            read.Session.LeagueSeasonId,
            read.Session.LeagueId,
            Skip = (request.Page - 1) * request.PageSize,
            request.PageSize
        };
        if (request.TeamId is not null)
        {
            var exists = await read.Connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                SELECT COUNT(*) FROM Teams
                WHERE Id = @TeamId AND LeagueSeasonId = @LeagueSeasonId AND LeagueId = @LeagueId
                """, parameters, read.Transaction, cancellationToken: ct));
            if (exists == 0) throw AuctionReadSession.NotFound();
        }

        // La rosa appartiene alla stagione; ogni acquisto conserva il suo listone originario.
        using var results = await read.Connection.QueryMultipleAsync(new CommandDefinition("""
            SELECT COUNT(*) FROM RosterEntries
            WHERE LeagueSeasonId = @LeagueSeasonId AND LeagueId = @LeagueId AND (@TeamId IS NULL OR TeamId = @TeamId);
            SELECT r.PlayerId, r.TeamId, r.PlayerAuctionId, entry.Name, r.Role, entry.ClubName, r.Price, r.AcquiredAt
            FROM RosterEntries r
            INNER JOIN PlayerAuctions auction ON auction.Id = r.PlayerAuctionId
              AND auction.LeagueSeasonId = r.LeagueSeasonId AND auction.LeagueId = r.LeagueId
            INNER JOIN ListEntries entry ON entry.ListVersionId = auction.ListVersionId AND entry.PlayerId = r.PlayerId
            WHERE r.LeagueSeasonId = @LeagueSeasonId AND r.LeagueId = @LeagueId AND (@TeamId IS NULL OR r.TeamId = @TeamId)
            ORDER BY r.AcquiredAt DESC, r.PlayerId
            OFFSET @Skip ROWS FETCH NEXT @PageSize ROWS ONLY;
            """, parameters, read.Transaction, cancellationToken: ct));
        var total = await results.ReadSingleAsync<int>();
        var items = (await results.ReadAsync<AuctionRosterView>()).AsList();
        await read.CommitAsync(ct);
        return new(items, request.Page, request.PageSize, total);
    }
}
