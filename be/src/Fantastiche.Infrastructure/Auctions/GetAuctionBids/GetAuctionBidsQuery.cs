using Fantastiche.Core.Auth;
using Fantastiche.Infrastructure.Common;

namespace Fantastiche.Infrastructure.Auctions;

public sealed record GetAuctionBidsQuery(
    RequestContext Context,
    Guid SessionId,
    Guid PlayerAuctionId,
    int Page = 1,
    int PageSize = 50) : IRequest<AuctionPage<AuctionBidView>>;
