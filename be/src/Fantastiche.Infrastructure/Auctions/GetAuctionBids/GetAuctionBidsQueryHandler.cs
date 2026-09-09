using Dapper;
using Fantastiche.Infrastructure.Common;
using Fantastiche.Infrastructure.Common.Persistence;

namespace Fantastiche.Infrastructure.Auctions;

public sealed class GetAuctionBidsQueryHandler(FantasticheDbContext db) : IRequestHandler<GetAuctionBidsQuery, AuctionPage<AuctionBidView>>
{
    public async Task<AuctionPage<AuctionBidView>> HandleAsync(GetAuctionBidsQuery request, CancellationToken ct)
    {
        AuctionReadSession.ValidatePage(request.Page, request.PageSize);
        await using var read = await AuctionReadSession.OpenAsync(db, request.Context, request.SessionId, ct);
        var parameters = new
        {
            request.SessionId,
            request.PlayerAuctionId,
            read.Session.LeagueSeasonId,
            read.Session.LeagueId,
            Skip = (request.Page - 1) * request.PageSize,
            request.PageSize
        };
        var exists = await read.Connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM PlayerAuctions
            WHERE Id = @PlayerAuctionId AND SessionId = @SessionId
              AND LeagueSeasonId = @LeagueSeasonId AND LeagueId = @LeagueId
            """, parameters, read.Transaction, cancellationToken: ct));
        if (exists == 0) throw AuctionReadSession.NotFound();

        using var results = await read.Connection.QueryMultipleAsync(new CommandDefinition("""
            SELECT COUNT(*) FROM Bids
            WHERE PlayerAuctionId = @PlayerAuctionId AND LeagueSeasonId = @LeagueSeasonId AND LeagueId = @LeagueId;
            SELECT Id, PlayerAuctionId, TeamId, UserId, Amount, Sequence, AcceptedAt FROM Bids
            WHERE PlayerAuctionId = @PlayerAuctionId AND LeagueSeasonId = @LeagueSeasonId AND LeagueId = @LeagueId
            ORDER BY Sequence DESC, Id
            OFFSET @Skip ROWS FETCH NEXT @PageSize ROWS ONLY;
            """, parameters, read.Transaction, cancellationToken: ct));
        var total = await results.ReadSingleAsync<int>();
        var items = (await results.ReadAsync<AuctionBidView>()).AsList();
        await read.CommitAsync(ct);
        return new(items, request.Page, request.PageSize, total);
    }
}
