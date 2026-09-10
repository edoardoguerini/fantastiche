using Fantastiche.Application.Infrastructure.Http;
using Fantastiche.Application.Infrastructure.Modules;
using Fantastiche.Infrastructure.Common;
using Fantastiche.Infrastructure.Leagues;
using FluentValidation;
namespace Fantastiche.Application.Modules.Leagues;

public sealed class LeaguesModule : IRegistrableModule
{
    public void RegisterEndpoints(IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/Leagues").WithTags("Leagues").RequireAuthorization();
        group.MapGet("", GetAll).WithName("GetLeagues")
         .WithSummary("Le mie leghe").WithDescription("Elenco paginato delle stagioni delle leghe attive per l’utente. Il SuperAdmin vede tutte le leghe.")
         .Produces<ApiResponse<LeaguePage<LeagueDetails>>>();
        group.MapPost("/", Create).RequireAuthorization("SuperAdmin").WithName("CreateLeague")
         .WithSummary("Crea lega").WithDescription("Crea lega e stagione e invita l’organizzatore. Solo SuperAdmin.");
        group.MapGet("/{leagueId:guid}", Get).WithName("GetLeague")
         .WithSummary("Dettaglio lega").WithDescription("Legge la configurazione della lega. Membri attivi e SuperAdmin.");
        group.MapPost("/{leagueId:guid}/Invitations", Invite).WithName("InviteMember")
         .WithSummary("Invita partecipante").WithDescription("Accoda l’invito personale a una stagione. Organizzatore o SuperAdmin.");
        group.MapPost("/{leagueId:guid}/Invitations/{invitationId:guid}/Revoke", Revoke).WithName("RevokeInvitation")
         .WithSummary("Revoca invito").WithDescription("Invalida un invito ancora non accettato. Organizzatore o SuperAdmin.");
        group.MapPost("/{leagueId:guid}/Invitations/{invitationId:guid}/Resend", Resend).WithName("ResendInvitation")
         .WithSummary("Reinvia invito").WithDescription("Genera un nuovo invito e revoca il precedente. Organizzatore o SuperAdmin.");
    }
    private static async Task<IResult> GetAll(int? page, int? pageSize, HttpContext context,
        IValidator<LeagueListRequest> validator, IRequestPublisher publisher, CancellationToken ct)
    {
        var request = new LeagueListRequest(page ?? 1, pageSize ?? 20);
        await validator.ValidateAndThrowAsync(request, ct);
        return ApiResults.Ok(await publisher.QueryAsync<GetLeaguesQuery, LeaguePage<LeagueDetails>>(
            new(context.CreateRequestContext(), request.Page, request.PageSize), ct));
    }
    private static async Task<IResult> Create(CreateLeagueRequest r, HttpContext context, IValidator<CreateLeagueRequest> validator, IRequestPublisher publisher, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(r, ct);
        var result = await publisher.SendAsync<CreateLeagueCommand, LeagueDetails>(new(context.CreateRequestContext(), r.Name, r.SeasonName, r.OrganizerEmail, r.OrganizerName, r.Budget, r.Goalkeepers, r.Defenders, r.Midfielders, r.Forwards), ct);
        return ApiResults.Created($"/api/Leagues/{result.Id}", result);
    }
    private static async Task<IResult> Get(Guid leagueId, HttpContext context, IRequestPublisher publisher, CancellationToken ct)
     => ApiResults.Ok(await publisher.QueryAsync<GetLeagueQuery, LeagueDetails>(new(context.CreateRequestContext(), leagueId), ct));
    private static async Task<IResult> Invite(Guid leagueId, InviteMemberRequest r, HttpContext context, IValidator<InviteMemberRequest> validator, IRequestPublisher publisher, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(r, ct);
        var result = await publisher.SendAsync<InviteMemberCommand, InvitationDetails>(new(context.CreateRequestContext(), leagueId, r.LeagueSeasonId, r.Email, r.DisplayName), ct);
        return ApiResults.Created($"/api/Leagues/{leagueId}/Invitations/{result.Id}", result);
    }
    private static async Task<IResult> Revoke(Guid leagueId, Guid invitationId, HttpContext context, IRequestPublisher publisher, CancellationToken ct)
     => ApiResults.Ok(await publisher.SendAsync<RevokeInvitationCommand, bool>(new(context.CreateRequestContext(), leagueId, invitationId), ct));
    private static async Task<IResult> Resend(Guid leagueId, Guid invitationId, HttpContext context, IRequestPublisher publisher, CancellationToken ct)
     => ApiResults.Ok(await publisher.SendAsync<ResendInvitationCommand, InvitationDetails>(new(context.CreateRequestContext(), leagueId, invitationId), ct));
}
