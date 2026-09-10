using Fantastiche.Application.Infrastructure.Http;
using Fantastiche.Application.Infrastructure.Modules;
using Fantastiche.Infrastructure.Common;
using Fantastiche.Infrastructure.Leagues;
using FluentValidation;

namespace Fantastiche.Application.Modules.Leagues;

public sealed class LeagueParticipantsModule : IRegistrableModule
{
    public void RegisterEndpoints(IEndpointRouteBuilder api)
    {
        api.MapGet("/Leagues/{leagueId:guid}/Seasons/{leagueSeasonId:guid}/Participants", Get)
            .WithTags("Leagues").RequireAuthorization().WithName("GetLeagueParticipants")
            .WithSummary("Partecipanti e inviti della stagione")
            .WithDescription("Restituisce canManage dal database; solo organizzatori attivi e SuperAdmin ricevono partecipanti e inviti paginati, senza token. page=1, pageSize=20.")
            .Produces<ApiResponse<LeagueParticipantsView>>();
    }

    private static async Task<IResult> Get(Guid leagueId, Guid leagueSeasonId, int? page, int? pageSize,
        HttpContext context, IValidator<LeagueParticipantsRequest> validator, IRequestPublisher publisher, CancellationToken ct)
    {
        var request = new LeagueParticipantsRequest(page ?? 1, pageSize ?? 20);
        await validator.ValidateAndThrowAsync(request, ct);
        return ApiResults.Ok(await publisher.QueryAsync<GetLeagueParticipantsQuery, LeagueParticipantsView>(
            new(context.CreateRequestContext(), leagueId, leagueSeasonId, request.Page, request.PageSize), ct));
    }
}

public sealed record LeagueParticipantsRequest(int Page, int PageSize);
public sealed class LeagueParticipantsRequestValidator : AbstractValidator<LeagueParticipantsRequest>
{
    public LeagueParticipantsRequestValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 10000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
