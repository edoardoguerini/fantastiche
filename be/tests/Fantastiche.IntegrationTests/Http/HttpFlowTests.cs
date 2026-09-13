using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Fantastiche.Infrastructure.Common.Persistence;
using Fantastiche.Infrastructure.Emails;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
namespace Fantastiche.IntegrationTests.Http;

public sealed partial class HttpFlowTests(SqlFixture fixture) : IClassFixture<SqlFixture>
{
    private const string Password = "Invited-User-123!";
    private WebApplicationFactory<Program> Factory(TimeProvider? clock = null) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(fixture.Settings));
        // La registrazione iniziale del minimal host precede ConfigureAppConfiguration del test host.
        // Sostituzione esplicita: nessuna richiesta del test può usare il DB applicativo.
        builder.ConfigureServices(services =>
     {
         services.RemoveAll<DbContextOptions<FantasticheDbContext>>();
         services.AddDbContext<FantasticheDbContext>(options => options.UseSqlServer(fixture.ConnectionString));
         services.RemoveAll<IDataProtectionProvider>();
         services.AddSingleton(fixture.Services.GetRequiredService<IDataProtectionProvider>());
         if (clock is not null)
         {
             services.Configure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme,
                 options => options.TimeProvider = clock);
             services.Configure<SecurityStampValidatorOptions>(options => options.TimeProvider = clock);
         }
     });
    });
    private static HttpClient Client(WebApplicationFactory<Program> factory) => factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    private static async Task RefreshCsrf(HttpClient client)
    {
        var json = await client.GetFromJsonAsync<JsonElement>("/api/Auth/Antiforgery");
        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", json.GetProperty("data").GetProperty("token").GetString());
    }
    private static async Task Login(HttpClient client, string email, string password)
    {
        await RefreshCsrf(client);
        var result = await client.PostAsJsonAsync("/api/Auth/Login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, result.StatusCode); await RefreshCsrf(client);
    }
    private async Task<string> Token(Guid invitationId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var email = await scope.ServiceProvider.GetRequiredService<FantasticheDbContext>().EmailMessages.SingleAsync(x => x.InvitationId == invitationId);
        var text = scope.ServiceProvider.GetRequiredService<EmailPayloadProtector>().Unprotect(email.ProtectedPayload).TextBody!;
        return Regex.Match(text, "#token=([0-9a-fA-F]{64})").Groups[1].Value;
    }
    private static async Task<JsonElement> Data(HttpResponseMessage result, HttpStatusCode status)
    {
        var text = await result.Content.ReadAsStringAsync(); Assert.True(result.StatusCode == status, $"Atteso {status}, ricevuto {result.StatusCode}: {text}");
        return JsonDocument.Parse(text).RootElement.GetProperty("data").Clone();
    }
    [Fact]
    public async Task HttpFlowCreatesLeagueActivatesOrganizerAndParticipantThenLogsOut()
    {
        await using var factory = Factory(); using var admin = Client(factory); using var organizer = Client(factory); using var participant = Client(factory);
        await Login(admin, "admin@example.test", "Test-Admin-123!");
        var organizerEmail = Guid.NewGuid() + "@example.test";
        var league = await Data(await admin.PostAsJsonAsync("/api/Leagues/", new { name = "Lega HTTP", seasonName = "2026/27", organizerEmail }), HttpStatusCode.Created);
        var leagueId = league.GetProperty("id").GetGuid(); var season = league.GetProperty("leagueSeasonId").GetGuid();
        Guid invitationId;
        await using (var scope = fixture.Services.CreateAsyncScope()) invitationId = (await scope.ServiceProvider.GetRequiredService<FantasticheDbContext>().LeagueInvitations.SingleAsync(x => x.LeagueId == leagueId)).Id;
        var token = await Token(invitationId);
        using (var preview = new HttpRequestMessage(HttpMethod.Get, "/api/Invitations/Preview"))
        {
            preview.Headers.Add("X-Invitation-Token", token);
            var json = await Data(await organizer.SendAsync(preview), HttpStatusCode.OK); Assert.False(json.GetProperty("requiresTeam").GetBoolean());
            Assert.Equal("Lega HTTP", json.GetProperty("leagueName").GetString()); Assert.Equal("Admin", json.GetProperty("invitedBy").GetString());
            Assert.Equal(JsonValueKind.Null, json.GetProperty("leagueLogoUrl").ValueKind);
            var hint = json.GetProperty("recipientEmailHint").GetString()!;
            Assert.StartsWith(organizerEmail[..1] + "•••", hint); Assert.EndsWith("@example.test", hint); Assert.DoesNotContain(organizerEmail, hint);
        }
        await RefreshCsrf(organizer);
        await Data(await organizer.PostAsJsonAsync("/api/Invitations/Accept", new { token, password = Password }), HttpStatusCode.BadRequest);
        await Data(await organizer.PostAsJsonAsync("/api/Invitations/Accept", new { token, password = Password, displayName = new string('x', 151) }), HttpStatusCode.BadRequest);
        await Data(await organizer.PostAsJsonAsync("/api/Invitations/Accept", new { token, password = Password, displayName = "Organizer" }), HttpStatusCode.OK);
        await Login(organizer, organizerEmail, Password);
        var email = Guid.NewGuid() + "@example.test";
        var invite = await Data(await organizer.PostAsJsonAsync($"/api/Leagues/{leagueId}/Invitations", new { leagueSeasonId = season, email }), HttpStatusCode.Created);
        token = await Token(invite.GetProperty("id").GetGuid());
        await RefreshCsrf(participant);
        var acceptance = await Data(await participant.PostAsJsonAsync("/api/Invitations/Accept", new { token, password = Password, displayName = "Player", teamName = "Squadra HTTP" }), HttpStatusCode.OK);
        Assert.NotEqual(Guid.Empty, acceptance.GetProperty("teamId").GetGuid());
        Assert.Equal("Squadra HTTP", acceptance.GetProperty("teamName").GetString()); Assert.Equal(email, acceptance.GetProperty("email").GetString());
        await Login(participant, email, Password);
        var detail = await Data(await participant.GetAsync($"/api/Leagues/{leagueId}"), HttpStatusCode.OK); Assert.Equal(500, detail.GetProperty("budget").GetInt32());
        var denied = await participant.PostAsJsonAsync("/api/Leagues/", new { name = "Vietata" }); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await Data(await participant.PostAsJsonAsync("/api/Auth/Logout", new { }), HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.Unauthorized, (await participant.GetAsync("/api/Auth/Me")).StatusCode);
    }
    [Fact]
    public async Task JsonMutationsRequireValidAntiforgeryIncludingAnonymousLogin()
    {
        await using var factory = Factory(); using var client = Client(factory);
        var response = await client.PostAsJsonAsync("/api/Auth/Login", new { email = "admin@example.test", password = "Test-Admin-123!" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(); Assert.False(body.GetProperty("isSuccess").GetBoolean());
        await RefreshCsrf(client); client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN"); client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", "invalid");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/Invitations/Accept", new { token = new string('A', 64), password = Password, teamName = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/Auth/Me")).StatusCode);
    }
    [Fact]
    public async Task InvalidCredentialsAndMalformedJsonReturnSafeEnvelopes()
    {
        await using var factory = Factory(); using var client = Client(factory); await RefreshCsrf(client);
        var invalid = await client.PostAsJsonAsync("/api/Auth/Login", new { email = "missing@example.test", password = Password }); Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        var json = await invalid.Content.ReadFromJsonAsync<JsonElement>(); Assert.False(json.GetProperty("isSuccess").GetBoolean());
        var malformed = await client.PostAsync("/api/Auth/Login", new StringContent("{", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
    }
    [Fact]
    public async Task HealthAndOpenApiExposeConfiguredEndpoints()
    {
        await using var factory = Factory(); using var client = Client(factory);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
        var schema = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        Assert.True(schema.GetProperty("paths").TryGetProperty("/api/Invitations/Accept", out _));
        Assert.True(schema.GetProperty("paths").TryGetProperty("/api/Auth/Login", out _));
    }
    [Theory]
    [InlineData("/scalar")]
    [InlineData("/scalar/v1")]
    public async Task ScalarServesApiReferenceInDevelopment(string path)
    {
        await using var factory = Factory();
        using var client = Client(factory);
        using var initial = await client.GetAsync(path);
        using var response = initial.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.TemporaryRedirect or HttpStatusCode.MovedPermanently
            ? await client.GetAsync(initial.Headers.Location)
            : await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Fantastiche API", html);
        Assert.Contains("/openapi/v1.json", html);
    }

}
