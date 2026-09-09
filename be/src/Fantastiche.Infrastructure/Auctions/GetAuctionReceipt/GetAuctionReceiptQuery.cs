using Fantastiche.Core.Auth;
using Fantastiche.Infrastructure.Common;

namespace Fantastiche.Infrastructure.Auctions;

public sealed record GetAuctionReceiptQuery(RequestContext Context, Guid SessionId, Guid RequestId)
    : IRequest<AuctionCommandResult>;
