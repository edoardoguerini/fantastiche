using Fantastiche.Core.Exceptions;
using Fantastiche.Infrastructure.Common.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Fantastiche.UnitTests.Common.Authentication;

public sealed class LoginCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_UsaLockoutESchermaturaCredenzialiNonValide()
    {
        var user = new ApplicationUser { Email = "user@example.test" };
        var userManager = CreateUserManager();
        var signInManager = CreateSignInManager(userManager);
        userManager.FindByEmailAsync(user.Email).Returns(user);
        signInManager.PasswordSignInAsync(user, "password-errata", false, true)
            .Returns(SignInResult.Failed);
        var handler = new LoginCommandHandler(userManager, signInManager);

        var error = await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(
            new LoginCommand(user.Email, "password-errata"),
            default));

        Assert.Equal("auth.invalid_credentials", error.Code);
        Assert.Equal(StatusCodes.Status401Unauthorized, error.StatusCode);
        await signInManager.Received(1)
            .PasswordSignInAsync(user, "password-errata", false, true);
    }

    private static UserManager<ApplicationUser> CreateUserManager()
    {
        return Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(),
            Options.Create(new IdentityOptions()),
            Substitute.For<IPasswordHasher<ApplicationUser>>(),
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            Substitute.For<ILookupNormalizer>(),
            new IdentityErrorDescriber(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<UserManager<ApplicationUser>>>());
    }

    private static SignInManager<ApplicationUser> CreateSignInManager(UserManager<ApplicationUser> userManager)
    {
        return Substitute.For<SignInManager<ApplicationUser>>(
            userManager,
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            Options.Create(new IdentityOptions()),
            Substitute.For<ILogger<SignInManager<ApplicationUser>>>(),
            Substitute.For<IAuthenticationSchemeProvider>(),
            Substitute.For<IUserConfirmation<ApplicationUser>>());
    }
}
