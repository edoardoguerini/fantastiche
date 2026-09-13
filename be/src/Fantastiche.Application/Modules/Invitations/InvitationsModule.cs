using Fantastiche.Application.Infrastructure.Http;
using Fantastiche.Application.Infrastructure.Modules;
using Fantastiche.Infrastructure.Common;
using Fantastiche.Infrastructure.Leagues;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
namespace Fantastiche.Application.Modules.Invitations;

public sealed class InvitationsModule : IRegistrableModule
{
    public void RegisterEndpoints(IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/Invitations").WithTags("Invitations").AllowAnonymous();
        group.MapGet("/Preview", Preview).WithName("PreviewInvitation")
         .WithSummary("Anteprima invito").WithDescription("Mostra nome e logo della lega, chi invita, email mascherata del destinatario, scadenza e requisiti del form senza consumare l’invito. Token nell’header X-Invitation-Token.");
        group.MapPost("/Accept", Accept).WithName("AcceptInvitation")
         .WithSummary("Accetta invito").WithDescription("Attiva l’adesione e, per i partecipanti, crea la squadra. Un account esistente deve autenticarsi.");
    }
    private static async Task<IResult> Preview([FromHeader(Name = "X-Invitation-Token")] string token, HttpContext context, IRequestPublisher publisher, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "no-store";
        return ApiResults.Ok(await publisher.QueryAsync<GetInvitationQuery, InvitationPreview>(new(context.CreateRequestContext(), token), ct));
    }
    private static async Task<IResult> Accept(AcceptInvitationRequest r, HttpContext context, IValidator<AcceptInvitationRequest> validator, IRequestPublisher publisher, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(r, ct);
        return ApiResults.Ok(await publisher.SendAsync<AcceptInvitationCommand, AcceptanceDetails>(new(context.CreateRequestContext(), r.Token, r.Password, r.TeamName, r.DisplayName), ct));
    }
}
