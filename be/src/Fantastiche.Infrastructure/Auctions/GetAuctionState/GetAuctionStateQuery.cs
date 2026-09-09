using Fantastiche.Core.Auth;
using Fantastiche.Infrastructure.Common;

namespace Fantastiche.Infrastructure.Auctions;

public sealed record GetAuctionStateQuery(RequestContext Context, Guid SessionId) : IRequest<AuctionSessionView>;
