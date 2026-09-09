using Fantastiche.Infrastructure.Common;
using Fantastiche.Infrastructure.Common.Persistence;

namespace Fantastiche.Infrastructure.Auctions;

public sealed class GetActiveAuctionQueryHandler(FantasticheDbContext db) : IRequestHandler<GetActiveAuctionQuery, AuctionSessionView?>
{
    public async Task<AuctionSessionView?> HandleAsync(GetActiveAuctionQuery request, CancellationToken ct)
    {
        await using var read = await AuctionReadSession.OpenActiveAsync(
            db, request.Context, request.LeagueId, request.LeagueSeasonId, ct);
        if (read is null) return null;
        var result = await read.ReadStateAsync(ct);
        await read.CommitAsync(ct);
        return result;
    }
}
