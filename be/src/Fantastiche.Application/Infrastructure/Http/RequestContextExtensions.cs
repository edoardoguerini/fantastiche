using System.Security.Claims;
using Fantastiche.Core.Auth;
using Fantastiche.Infrastructure.Common.Authentication;

namespace Fantastiche.Application.Infrastructure.Http;

public static class RequestContextExtensions
{
    public static RequestContext CreateRequestContext(this HttpContext context)
    {
        var identifier = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userId = Guid.TryParse(identifier, out var parsed) ? parsed : (Guid?)null;
        return new RequestContext(
            userId,
            context.User.IsInRole(FantasticheRoles.SuperAdmin),
            context.TraceIdentifier);
    }
}
