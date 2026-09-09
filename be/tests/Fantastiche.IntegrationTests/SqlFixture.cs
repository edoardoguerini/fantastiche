using Fantastiche.Core.Auth;
using Fantastiche.Infrastructure;
using Fantastiche.Infrastructure.Common.Authentication;
using Fantastiche.Infrastructure.Common.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace Fantastiche.IntegrationTests;

public sealed class SqlFixture : IAsyncLifetime
{
    public ServiceProvider Services { get; private set; } = null!;
    public string ConnectionString { get; private set; } = "";
    public RequestContext Admin { get; private set; } = null!;
    public Dictionary<string, string?> Settings { get; private set; } = null!;
    private readonly string database = "Fantastiche_Test_" + Guid.NewGuid().ToString("N");
    public async Task InitializeAsync()
    {
        var source = Environment.GetEnvironmentVariable("ConnectionStrings__Fantastiche")
         ?? throw new InvalidOperationException("Avvia `just be up`, poi usa `just be test-int`: SQL Server reale è obbligatorio.");
        var connection = new SqlConnectionStringBuilder(source) { InitialCatalog = database };
        if (connection.DataSource is not ("127.0.0.1,14333" or "localhost,14333"))
            throw new InvalidOperationException("I test sono limitati a SQL Server Fantastiche locale sulla porta 14333.");
        ConnectionString = connection.ConnectionString;
        Settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Fantastiche"] = ConnectionString,
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["DOTNET_ENVIRONMENT"] = "Development",
            ["Email:Provider"] = "Local",
            ["Email:LocalDirectory"] = Path.GetFullPath(".local/test-mail/" + database),
            ["DataProtection:KeyPath"] = Path.GetFullPath(".local/test-keys/" + database)
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(Settings).Build();
        var services = new ServiceCollection().AddLogging().AddSingleton<IConfiguration>(configuration);
        services.AddFantasticheInfrastructure(configuration);
        Services = services.BuildServiceProvider();
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FantasticheDbContext>();
        await db.Database.MigrateAsync();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { Email = "admin@example.test", UserName = "admin@example.test", DisplayName = "Admin", EmailConfirmed = true };
        var result = await users.CreateAsync(user, "Test-Admin-123!");
        Assert.True(result.Succeeded, string.Join(",", result.Errors.Select(e => e.Code)));
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        Assert.True((await roles.CreateAsync(new IdentityRole<Guid>("SuperAdmin") { Id = Guid.CreateVersion7() })).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, "SuperAdmin")).Succeeded);
        Admin = new RequestContext(user.Id, true, "integration");
    }
    public async Task DisposeAsync()
    {
        if (Services is null) return;
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FantasticheDbContext>();
        Assert.Equal(database, new SqlConnectionStringBuilder(db.Database.GetConnectionString()).InitialCatalog);
        Assert.StartsWith("Fantastiche_Test_", database);
        await db.Database.EnsureDeletedAsync();
        await Services.DisposeAsync();
    }
}
