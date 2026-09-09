using Fantastiche.Infrastructure.Common;

namespace Fantastiche.Infrastructure.Auctions;

public sealed class StartPlayerAuctionCommandHandler(AuctionEngine engine) : IRequestHandler<StartPlayerAuctionCommand, AuctionCommandResult>
{
    public Task<AuctionCommandResult> HandleAsync(StartPlayerAuctionCommand request, CancellationToken ct) => engine.StartAsync(request, ct);
}
