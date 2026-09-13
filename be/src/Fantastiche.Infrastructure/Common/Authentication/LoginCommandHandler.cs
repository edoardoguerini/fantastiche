using Fantastiche.Core.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace Fantastiche.Infrastructure.Common.Authentication;

public sealed class LoginCommandHandler(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager)
    : IRequestHandler<LoginCommand, AuthenticatedUser>
{
    public async Task<AuthenticatedUser> HandleAsync(
        LoginCommand request,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            throw InvalidCredentials();
        }

        var result = await signInManager.PasswordSignInAsync(
            user,
            request.Password,
            isPersistent: true,
            lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            throw new DomainException(
                "auth.locked_out",
                "Account temporaneamente bloccato.",
                StatusCodes.Status423Locked);
        }

        if (!result.Succeeded)
        {
            throw InvalidCredentials();
        }

        return new AuthenticatedUser(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            await userManager.IsInRoleAsync(user, FantasticheRoles.SuperAdmin));
    }

    private static DomainException InvalidCredentials()
        => new(
            "auth.invalid_credentials",
            "Email o password non validi.",
            StatusCodes.Status401Unauthorized);
}
