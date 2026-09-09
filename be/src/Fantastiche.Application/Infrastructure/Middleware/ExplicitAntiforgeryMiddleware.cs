using Fantastiche.Application.Infrastructure.Http;
using Microsoft.AspNetCore.Antiforgery;

namespace Fantastiche.Application.Infrastructure.Middleware;

public sealed class ExplicitAntiforgeryMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery)
    {
        if (RequiresValidation(context.Request))
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                await ApiResults.WriteErrorAsync(
                    context,
                    StatusCodes.Status400BadRequest,
                    "security.antiforgery",
                    "Token antiforgery mancante o non valido.",
                    context.RequestAborted);
                return;
            }
        }

        await next(context);
    }

    private static bool RequiresValidation(HttpRequest request)
        => request.Path.StartsWithSegments("/api")
            && (HttpMethods.IsPost(request.Method)
                || HttpMethods.IsPut(request.Method)
                || HttpMethods.IsPatch(request.Method)
                || HttpMethods.IsDelete(request.Method));
}
