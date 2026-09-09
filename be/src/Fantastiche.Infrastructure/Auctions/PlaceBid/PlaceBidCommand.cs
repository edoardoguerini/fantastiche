using Fantastiche.Core.Auth;
using Fantastiche.Infrastructure.Common;

namespace Fantastiche.Infrastructure.Auctions;

public sealed record PlaceBidCommand(
    RequestContext Context,
    Guid SessionId,
    Guid RequestId,
    Guid PlayerAuctionId,
    int Amount) : IRequest<AuctionCommandResult>;
