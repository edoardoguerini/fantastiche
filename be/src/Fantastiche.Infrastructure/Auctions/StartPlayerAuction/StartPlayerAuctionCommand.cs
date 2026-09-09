using Fantastiche.Core.Auth;
using Fantastiche.Infrastructure.Common;

namespace Fantastiche.Infrastructure.Auctions;

public sealed record StartPlayerAuctionCommand(
    RequestContext Context,
    Guid SessionId,
    Guid RequestId,
    Guid PlayerId,
    int DurationSeconds,
    IReadOnlyList<int> Increments) : IRequest<AuctionCommandResult>;
