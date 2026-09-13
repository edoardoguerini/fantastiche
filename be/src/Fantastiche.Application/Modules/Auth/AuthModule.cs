using System.Security.Claims;
using Fantastiche.Application.Infrastructure.Http;
using Fantastiche.Application.Infrastructure.Modules;
using Fantastiche.Core.Exceptions;
using Fantastiche.Infrastructure.Common;
using Fantastiche.Infrastructure.Common.Authentication;
using FluentValidation;
using Microsoft.AspNetCore.Antiforgery;

namespace Fantastiche.Application.Modules.Auth;

public sealed class AuthModule : IRegistrableModule
{
    public void RegisterEndpoints(IEndpointRouteBuilder api)
    {
        var auth = api.MapGroup("/Auth").WithTags("Auth");

        auth.MapGet("/Antiforgery", GetAntiforgeryToken)
            .AllowAnonymous()
            .WithName("GetAntiforgeryToken")
            .WithSummary("Genera un token antiforgery")
            .WithDescription("Restituisce il token da inviare nell'header X-XSRF-TOKEN delle richieste mutative.");

        auth.MapPost("/Login", LoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting("authentication")
            .WithName("Login")
            .WithSummary("Accede con cookie Identity")
            .WithDescription("Verifica le credenziali e crea una sessione cookie persistente di sette giorni, rinnovabile fino a trenta giorni dal login.");

        auth.MapPost("/Logout", LogoutAsync)
            .RequireAuthorization()
            .WithName("Logout")
            .WithSummary("Termina la sessione corrente")
            .WithDescription("Revoca il cookie della sessione corrente.");

        auth.MapGet("/Me", GetCurrentUserAsync)
            .RequireAuthorization()
            .WithName("GetCurrentUser")
            .WithSummary("Restituisce l'utente corrente")
            .WithDescription("Restituisce identità e ruolo globale della sessione autenticata.");
    }

    private static IResult GetAntiforgeryToken(HttpContext context, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        return ApiResults.Ok(new AntiforgeryToken(tokens.RequestToken ?? string.Empty));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IValidator<LoginRequest> validator,
        IRequestPublisher publisher,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await publisher.SendAsync<LoginCommand, AuthenticatedUser>(
            new LoginCommand(request.Email, request.Password),
            cancellationToken);
        return ApiResults.Ok(result);
    }

    private static async Task<IResult> LogoutAsync(
        IRequestPublisher publisher,
        CancellationToken cancellationToken)
    {
        await publisher.SendAsync<LogoutCommand, bool>(new LogoutCommand(), cancellationToken);
        return ApiResults.Ok(new LogoutResult(true));
    }

    private static async Task<IResult> GetCurrentUserAsync(
        ClaimsPrincipal principal,
        IRequestPublisher publisher,
        CancellationToken cancellationToken)
    {
        var identifier = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(identifier, out var userId))
        {
            throw new DomainException("auth.required", "Accesso richiesto.", StatusCodes.Status401Unauthorized);
        }

        var result = await publisher.QueryAsync<GetCurrentUserQuery, AuthenticatedUser>(
            new GetCurrentUserQuery(userId),
            cancellationToken);
        return ApiResults.Ok(result);
    }

    private sealed record AntiforgeryToken(string Token);

    private sealed record LogoutResult(bool SignedOut);
}
