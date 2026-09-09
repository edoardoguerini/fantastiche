using Fantastiche.Application.Infrastructure.Authentication;
using Fantastiche.Infrastructure.Common.Authentication;
using Fantastiche.Infrastructure.Common.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace Fantastiche.IntegrationTests.Http;

public sealed class BootstrapTests(SqlFixture fixture) : IClassFixture<SqlFixture>
{
    [Fact]
    public async Task ExplicitBootstrapCreatesAdminAndNeverResetsExistingAccount()
    {
        var email = Guid.NewGuid() + "@example.test";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Bootstrap:Email"] = email,
            ["Bootstrap:Password"] = "Explicit-Admin-123!",
            ["Bootstrap:DisplayName"] = "Admin test"
        }).Build();
        Guid id;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider; var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            var bootstrap = new SuperAdminBootstrapper(services.GetRequiredService<FantasticheDbContext>(), users, services.GetRequiredService<RoleManager<IdentityRole<Guid>>>(), configuration);
            var user = await bootstrap.BootstrapAsync(); id = user.Id;
            Assert.NotEqual(Guid.Empty, id); Assert.True(user.EmailConfirmed); Assert.True(await users.IsInRoleAsync(user, "SuperAdmin"));
        }
        configuration["Bootstrap:Password"] = "Replacement-Admin-123!";
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider; var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            var bootstrap = new SuperAdminBootstrapper(services.GetRequiredService<FantasticheDbContext>(), users, services.GetRequiredService<RoleManager<IdentityRole<Guid>>>(), configuration);
            await Assert.ThrowsAsync<InvalidOperationException>(() => bootstrap.BootstrapAsync());
            var user = (await users.FindByIdAsync(id.ToString()))!;
            Assert.True(await users.CheckPasswordAsync(user, "Explicit-Admin-123!")); Assert.False(await users.CheckPasswordAsync(user, "Replacement-Admin-123!"));
        }
    }
}
