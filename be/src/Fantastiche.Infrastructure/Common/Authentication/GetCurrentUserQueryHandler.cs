using Fantastiche.Core.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace Fantastiche.Infrastructure.Common.Authentication;

public sealed class GetCurrentUserQueryHandler(UserManager<ApplicationUser> userManager)
    : IRequestHandler<GetCurrentUserQuery, AuthenticatedUser>
{
    public async Task<AuthenticatedUser> HandleAsync(
        GetCurrentUserQuery request,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
        {
            throw new DomainException(
                "auth.required",
                "Accesso richiesto.",
                StatusCodes.Status401Unauthorized);
        }

        return new AuthenticatedUser(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            await userManager.IsInRoleAsync(user, FantasticheRoles.SuperAdmin));
    }
}
