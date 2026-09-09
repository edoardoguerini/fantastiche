using Fantastiche.Infrastructure.Common.Authentication;
using Fantastiche.Infrastructure.Common.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Fantastiche.Application.Infrastructure.Authentication;

public sealed class SuperAdminBootstrapper(
    FantasticheDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IConfiguration configuration)
{
    public async Task<ApplicationUser> BootstrapAsync(CancellationToken cancellationToken = default)
    {
        var email = Required("Bootstrap:Email");
        var password = Required("Bootstrap:Password");
        var displayName = configuration["Bootstrap:DisplayName"]?.Trim();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            throw new InvalidOperationException("Esiste già un account con l'email indicata; nessun dato è stato modificato.");
        }

        if (!await roleManager.RoleExistsAsync(FantasticheRoles.SuperAdmin))
        {
            EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole<Guid>(FantasticheRoles.SuperAdmin)
            {
                Id = Guid.CreateVersion7(),
            }));
        }

        var user = new ApplicationUser
        {
            Email = email,
            UserName = email,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? email : displayName,
            EmailConfirmed = true,
        };

        EnsureSucceeded(await userManager.CreateAsync(user, password));
        EnsureSucceeded(await userManager.AddToRoleAsync(user, FantasticheRoles.SuperAdmin));
        await transaction.CommitAsync(cancellationToken);
        return user;
    }

    private string Required(string key)
    {
        var value = configuration[key]?.Trim();
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Configurazione obbligatoria mancante: {key}.")
            : value;
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (result.Succeeded)
        {
            return;
        }

        var codes = string.Join(", ", result.Errors.Select(error => error.Code));
        throw new InvalidOperationException($"Identity ha rifiutato il bootstrap: {codes}.");
    }
}
