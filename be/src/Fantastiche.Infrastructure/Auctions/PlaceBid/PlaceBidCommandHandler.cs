using Fantastiche.Infrastructure.Common;

namespace Fantastiche.Infrastructure.Auctions;

public sealed class PlaceBidCommandHandler(AuctionEngine engine) : IRequestHandler<PlaceBidCommand, AuctionCommandResult>
{
    public Task<AuctionCommandResult> HandleAsync(PlaceBidCommand request, CancellationToken ct) => engine.BidAsync(request, ct);
}
