using Microsoft.Extensions.Options;

namespace Fantastiche.Application.Infrastructure.Auctions;

public sealed class AuctionOriginMiddleware(
    RequestDelegate next,
    IOptions<AuctionRealtimeOptions> options)
{
    private readonly HashSet<string> allowedOrigins = new(
        options.Value.AllowedOrigins,
        StringComparer.OrdinalIgnoreCase);

    public Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments(Hubs.AuctionsHub.Route) &&
            context.Request.Headers.Origin.Count > 0 &&
            (context.Request.Headers.Origin.Count != 1 ||
             !allowedOrigins.Contains(context.Request.Headers.Origin[0]!)))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        return next(context);
    }
}
