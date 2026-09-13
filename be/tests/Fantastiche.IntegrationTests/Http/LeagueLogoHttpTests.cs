using System.Net;
using Fantastiche.Core.Storage;
using Fantastiche.Core.Exceptions;
using Fantastiche.Infrastructure.Emails;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net.Http.Json;
using Fantastiche.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fantastiche.IntegrationTests.Http;

public sealed partial class HttpFlowTests
{
    private const string LogoPng = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9Zl1sAAAAASUVORK5CYII=";

    [Fact]
    public async Task CreatedLogoIsStoredAndAppearsInLeagueAndInvitationEmail()
    {
        await using var factory = Factory();
        using var admin = Client(factory);
        await Login(admin, "admin@example.test", "Test-Admin-123!");
        var logo = Convert.FromBase64String(LogoPng);
        var result = await Data(await admin.PostAsJsonAsync("/api/Leagues/", new
        {
            name = "Lega con logo",
            seasonName = "2026/27",
            organizerEmail = Guid.NewGuid() + "@example.test",
            organizerName = "Organizzatore",
            logo
        }), HttpStatusCode.Created);
        var id = result.GetProperty("id").GetGuid();
        var url = result.GetProperty("logoUrl").GetString();
        Assert.NotNull(url);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FantasticheDbContext>();
        var league = await db.Leagues.SingleAsync(x => x.Id == id);
        Assert.StartsWith(id.ToString("D") + "/", league.LogoBlobName);
        try
        {
            using var http = new HttpClient();
            var stored = await http.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, stored.StatusCode);
            Assert.Equal("image/png", stored.Content.Headers.ContentType!.MediaType);
            Assert.Equal(logo, await stored.Content.ReadAsByteArrayAsync());
            var invite = await db.LeagueInvitations.SingleAsync(x => x.LeagueId == id);
            var email = await db.EmailMessages.SingleAsync(x => x.InvitationId == invite.Id);
            var payload = scope.ServiceProvider.GetRequiredService<EmailPayloadProtector>().Unprotect(email.ProtectedPayload);
            Assert.Contains(url, payload.HtmlBody);
            var detail = await Data(await admin.GetAsync($"/api/Leagues/{id}"), HttpStatusCode.OK);
            Assert.Equal(url, detail.GetProperty("logoUrl").GetString());
        }
        finally { await scope.ServiceProvider.GetRequiredService<ILeagueLogoStore>().DeleteAsync(league.LogoBlobName!, default); }
    }

    [Fact]
    public async Task StorageFailureRollsBackLeagueOrganizerAndInvitation()
    {
        var failingStore = new FailingLogoStore();
        await using var factory = Factory().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<ILeagueLogoStore>();
            services.AddSingleton<ILeagueLogoStore>(failingStore);
        }));
        using var admin = Client(factory);
        await Login(admin, "admin@example.test", "Test-Admin-123!");
        var email = Guid.NewGuid() + "@example.test";
        var name = "Upload fallito " + Guid.NewGuid();
        var result = await admin.PostAsJsonAsync("/api/Leagues/", new
        {
            name,
            seasonName = "2026/27",
            organizerEmail = email,
            organizerName = "Organizzatore",
            logo = LogoPng
        });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, result.StatusCode);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FantasticheDbContext>();
        Assert.False(await db.Leagues.AnyAsync(x => x.Name == name));
        Assert.False(await db.Users.AnyAsync(x => x.Email == email));
        Assert.False(await db.EmailMessages.AnyAsync(x => x.ToAddress == email));
        Assert.False(await db.LeagueInvitations.AnyAsync(x => x.Email == email));
        Assert.NotNull(failingStore.DeletedBlob);
    }

    [Fact]
    public async Task OversizedLogoIsRejectedBeforeAnyDatabaseWrites()
    {
        await using var factory = Factory();
        using var admin = Client(factory);
        await Login(admin, "admin@example.test", "Test-Admin-123!");
        var email = Guid.NewGuid() + "@example.test";
        var result = await admin.PostAsJsonAsync("/api/Leagues/", new
        {
            name = "Troppo grande",
            seasonName = "2026/27",
            organizerEmail = email,
            organizerName = "Organizzatore",
            logo = new byte[2 * 1024 * 1024 + 1]
        });
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Contains("league.logo_too_large", await result.Content.ReadAsStringAsync());
        await using var scope = fixture.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<FantasticheDbContext>().Users.AnyAsync(x => x.Email == email));
    }

    private sealed class FailingLogoStore : ILeagueLogoStore
    {
        public string? DeletedBlob { get; private set; }
        public Task UploadAsync(string blobName, byte[] content, string contentType, CancellationToken ct) =>
            throw new DomainException("league.logo_unavailable", "Storage non disponibile", 503);
        public Task DeleteAsync(string blobName, CancellationToken ct) { DeletedBlob = blobName; return Task.CompletedTask; }
    }

    [Theory]
    [InlineData("PHN2Zz48L3N2Zz4=")]
    [InlineData("")]
    public async Task InvalidLogoDoesNotCreateLeagueOrOrganizer(string logo)
    {
        await using var factory = Factory();
        using var admin = Client(factory);
        await Login(admin, "admin@example.test", "Test-Admin-123!");
        var email = Guid.NewGuid() + "@example.test";
        var name = "Logo non valido " + Guid.NewGuid();
        var result = await admin.PostAsJsonAsync("/api/Leagues/", new
        {
            name,
            seasonName = "2026/27",
            organizerEmail = email,
            organizerName = "Organizzatore",
            logo
        });
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FantasticheDbContext>();
        Assert.False(await db.Leagues.AnyAsync(x => x.Name == name));
        Assert.False(await db.Users.AnyAsync(x => x.Email == email));
    }
}
