using Fantastiche.Core.Auth;
using Fantastiche.Infrastructure.Common;

namespace Fantastiche.Infrastructure.Auctions;

public sealed record GetAuctionRosterQuery(
    RequestContext Context,
    Guid SessionId,
    Guid? TeamId = null,
    int Page = 1,
    int PageSize = 50) : IRequest<AuctionPage<AuctionRosterView>>;
