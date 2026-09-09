using Microsoft.AspNetCore.Identity;

namespace Fantastiche.Infrastructure.Common.Authentication;

public sealed class LogoutCommandHandler(SignInManager<ApplicationUser> signInManager)
    : IRequestHandler<LogoutCommand, bool>
{
    public async Task<bool> HandleAsync(
        LogoutCommand request,
        CancellationToken cancellationToken = default)
    {
        await signInManager.SignOutAsync();
        return true;
    }
}
