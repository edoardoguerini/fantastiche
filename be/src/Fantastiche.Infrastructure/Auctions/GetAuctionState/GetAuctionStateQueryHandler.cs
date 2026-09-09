using Fantastiche.Infrastructure.Common;
using Fantastiche.Infrastructure.Common.Persistence;

namespace Fantastiche.Infrastructure.Auctions;

public sealed class GetAuctionStateQueryHandler(FantasticheDbContext db) : IRequestHandler<GetAuctionStateQuery, AuctionSessionView>
{
    public async Task<AuctionSessionView> HandleAsync(GetAuctionStateQuery request, CancellationToken ct)
    {
        await using var read = await AuctionReadSession.OpenAsync(db, request.Context, request.SessionId, ct);
        var result = await read.ReadStateAsync(ct);
        await read.CommitAsync(ct);
        return result;
    }
}
