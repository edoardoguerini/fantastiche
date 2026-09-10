using Fantastiche.Core.Auth;
using Fantastiche.Infrastructure.Common;

namespace Fantastiche.Infrastructure.Auctions;

public sealed record GetAuctionRoomQuery(RequestContext Context, Guid LeagueId, Guid LeagueSeasonId)
    : IRequest<AuctionRoomView>;
