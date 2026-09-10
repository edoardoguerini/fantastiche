using Fantastiche.Core.Auth;
using Fantastiche.Infrastructure.Common;

namespace Fantastiche.Infrastructure.Auctions;

public sealed record StartBombCommand(RequestContext Context, Guid SessionId, Guid RequestId, Guid PlayerId) : IRequest<AuctionCommandResult>;

public sealed class StartBombCommandHandler(AuctionEngine engine) : IRequestHandler<StartBombCommand, AuctionCommandResult>
{
    public Task<AuctionCommandResult> HandleAsync(StartBombCommand request, CancellationToken ct) => engine.StartBombAsync(request, ct);
}
