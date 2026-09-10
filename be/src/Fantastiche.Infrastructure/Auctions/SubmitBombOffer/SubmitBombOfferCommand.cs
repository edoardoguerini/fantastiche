using Fantastiche.Core.Auth;
using Fantastiche.Infrastructure.Common;

namespace Fantastiche.Infrastructure.Auctions;

public sealed record SubmitBombOfferCommand(RequestContext Context, Guid SessionId, Guid RequestId, Guid BombAuctionId, int Round, int Amount) : IRequest<AuctionCommandResult>;

public sealed class SubmitBombOfferCommandHandler(AuctionEngine engine) : IRequestHandler<SubmitBombOfferCommand, AuctionCommandResult>
{
    public Task<AuctionCommandResult> HandleAsync(SubmitBombOfferCommand request, CancellationToken ct) => engine.BombBidAsync(request, ct);
}
