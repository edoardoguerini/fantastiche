using Fantastiche.Application.Infrastructure.Http;
using Fantastiche.Application.Infrastructure.Modules;
using Fantastiche.Infrastructure.Auctions;
using Fantastiche.Infrastructure.Common;

namespace Fantastiche.Application.Modules.Leagues;

public sealed class LeagueAuctionsModule : IRegistrableModule
{
    public void RegisterEndpoints(IEndpointRouteBuilder api)
    {
        api.MapGet("/Leagues/{leagueId:guid}/Seasons/{leagueSeasonId:guid}/Auction", Get)
            .WithTags("Auctions").RequireAuthorization().WithName("GetActiveAuction")
            .WithSummary("Sessione attiva della stagione")
            .WithDescription("Recupera la sessione Active o Paused, oppure null se assente. Richiede accesso alla lega.")
            .Produces<ApiResponse<AuctionSessionView?>>();
    }

    private static async Task<IResult> Get(Guid leagueId, Guid leagueSeasonId, HttpContext context, IRequestPublisher publisher, CancellationToken ct)
        => ApiResults.Ok(await publisher.QueryAsync<GetActiveAuctionQuery, AuctionSessionView?>(
            new(context.CreateRequestContext(), leagueId, leagueSeasonId), ct));
}
