using Fantastiche.Infrastructure.Common;
using Fantastiche.Infrastructure.Catalog;
using Fantastiche.Infrastructure.Common.Persistence;

namespace Fantastiche.Infrastructure.Auctions;

public sealed class GetActiveAuctionQueryHandler(FantasticheDbContext db, PlayerPhotoStorage photos, ClubLogoStorage logos) : IRequestHandler<GetActiveAuctionQuery, AuctionSessionView?>
{
    public async Task<AuctionSessionView?> HandleAsync(GetActiveAuctionQuery request, CancellationToken ct)
    {
        await using var read = await AuctionReadSession.OpenActiveAsync(
            db, request.Context, request.LeagueId, request.LeagueSeasonId, ct);
        if (read is null) return null;
        var result = await read.ReadStateAsync(photos, logos, ct);
        await read.CommitAsync(ct);
        return result;
    }
}
