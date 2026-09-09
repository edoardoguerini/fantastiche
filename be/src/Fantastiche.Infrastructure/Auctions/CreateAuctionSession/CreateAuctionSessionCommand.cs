using Fantastiche.Core.Auth;
using Fantastiche.Infrastructure.Common;

namespace Fantastiche.Infrastructure.Auctions;

public sealed record CreateAuctionSessionCommand(
    RequestContext Context,
    Guid LeagueId,
    Guid LeagueSeasonId,
    IReadOnlyList<Guid> TeamOrder) : IRequest<AuctionSessionView>;
