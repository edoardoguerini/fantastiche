using Fantastiche.Core.Auth;
using Fantastiche.Infrastructure.Common;

namespace Fantastiche.Infrastructure.Auctions;

public sealed record GetAuctionCatalogQuery(
    RequestContext Context,
    Guid SessionId,
    string? Search = null,
    string? Role = null,
    bool AvailableOnly = true,
    int Page = 1,
    int PageSize = 30) : IRequest<AuctionPage<AuctionCatalogPlayerView>>;
