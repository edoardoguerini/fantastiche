using Fantastiche.Application.Infrastructure.Http;
using Fantastiche.Application.Infrastructure.Modules;
using Fantastiche.Infrastructure.Catalog;
using Fantastiche.Infrastructure.Common;
using FluentValidation;

namespace Fantastiche.Application.Modules.Leagues;

public sealed class LeagueCatalogModule : IRegistrableModule
{
    public void RegisterEndpoints(IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/Leagues/{leagueId:guid}/Seasons/{leagueSeasonId:guid}/Catalog")
            .WithTags("Leagues").RequireAuthorization();
        group.MapGet("", Get).WithName("GetLeagueCatalog")
            .WithSummary("Listone della lega")
            .WithDescription("Versione selezionata per la stagione; null se ancora assente. Accesso ai membri attivi e al SuperAdmin.")
            .Produces<ApiResponse<ListVersionView?>>();
        group.MapPut("", Set).WithName("SetLeagueCatalog")
            .WithSummary("Scegli listone della lega")
            .WithDescription("Organizzatore o SuperAdmin sceglie una versione pubblicata della stessa stagione. Prima scelta o replay; sostituzione non ancora abilitata.")
            .Produces<ApiResponse<LeagueCatalogView>>();
    }

    private static async Task<IResult> Get(Guid leagueId, Guid leagueSeasonId, HttpContext context, IRequestPublisher publisher, CancellationToken ct)
        => ApiResults.Ok(await publisher.QueryAsync<GetLeagueCatalogQuery, ListVersionView?>(
            new(context.CreateRequestContext(), leagueId, leagueSeasonId), ct));

    private static async Task<IResult> Set(Guid leagueId, Guid leagueSeasonId, SetLeagueCatalogRequest request, HttpContext context,
        IValidator<SetLeagueCatalogRequest> validator, IRequestPublisher publisher, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        return ApiResults.Ok(await publisher.SendAsync<SetLeagueCatalogCommand, LeagueCatalogView>(
            new(context.CreateRequestContext(), leagueId, leagueSeasonId, request.ListVersionId), ct));
    }
}
