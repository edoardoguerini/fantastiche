using Microsoft.Extensions.Primitives;

namespace Fantastiche.Application.Infrastructure.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers[HeaderName];
        var correlationId = IsValid(supplied)
            ? supplied.ToString()
            : Guid.CreateVersion7().ToString("N");

        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;
        await next(context);
    }

    private static bool IsValid(StringValues value)
    {
        var text = value.ToString();
        return text.Length is > 0 and <= 128
            && text.All(character => !char.IsControl(character));
    }
}
