using Fantastiche.Infrastructure.Common;

namespace Fantastiche.Infrastructure.Auctions;

public sealed class ControlAuctionSessionCommandHandler(AuctionEngine engine) : IRequestHandler<ControlAuctionSessionCommand, AuctionCommandResult>
{
    public Task<AuctionCommandResult> HandleAsync(ControlAuctionSessionCommand request, CancellationToken ct) => engine.ControlAsync(request, ct);
}
