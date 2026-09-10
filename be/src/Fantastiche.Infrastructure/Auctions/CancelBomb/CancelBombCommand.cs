using Fantastiche.Core.Auth;
using Fantastiche.Infrastructure.Common;

namespace Fantastiche.Infrastructure.Auctions;

public sealed record CancelBombCommand(RequestContext Context, Guid SessionId, Guid RequestId, Guid BombAuctionId) : IRequest<AuctionCommandResult>;

public sealed class CancelBombCommandHandler(AuctionEngine engine) : IRequestHandler<CancelBombCommand, AuctionCommandResult>
{
    public Task<AuctionCommandResult> HandleAsync(CancelBombCommand request, CancellationToken ct) => engine.CancelBombAsync(request, ct);
}
