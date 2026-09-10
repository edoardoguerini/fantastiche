using Fantastiche.Core.Auth;
using Fantastiche.Infrastructure.Common;

namespace Fantastiche.Infrastructure.Auctions;

public sealed record ControlAuctionSessionCommand(
    RequestContext Context,
    Guid SessionId,
    Guid RequestId,
    string Action,
    IReadOnlyList<Guid>? TeamOrder = null,
    Guid? TargetTeamId = null) : IRequest<AuctionCommandResult>;
