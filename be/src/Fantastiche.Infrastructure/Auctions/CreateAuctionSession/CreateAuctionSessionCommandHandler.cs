using Fantastiche.Infrastructure.Common;

namespace Fantastiche.Infrastructure.Auctions;

public sealed class CreateAuctionSessionCommandHandler(AuctionEngine engine) : IRequestHandler<CreateAuctionSessionCommand, AuctionSessionView>
{
    public Task<AuctionSessionView> HandleAsync(CreateAuctionSessionCommand request, CancellationToken ct) => engine.CreateSessionAsync(request, ct);
}
