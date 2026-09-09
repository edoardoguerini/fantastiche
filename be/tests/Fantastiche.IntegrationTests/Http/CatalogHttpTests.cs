using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fantastiche.Infrastructure.Common.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Fantastiche.IntegrationTests.Http;

public sealed partial class HttpFlowTests
{
    private static string CatalogCsv(string name = "Portiere Uno") => string.Join("\n",
        string.Join(",", new[] { "101", name, "Giocatore Uno", "P", "Por", "x", "x", "x", "x", "Club Uno", "x", "x", "Destro", "Italia", "01/07/2000 00:00:00", "https://example.test/101.png", "x", "x", "x" }),
        string.Join(",", new[] { "102", "Attaccante Due", "Giocatore Due", "A", "Pc", "x", "x", "x", "x", "Club Due", "x", "x", "Sinistro", "Italia", "01/07/2001 00:00:00", "https://example.test/102.png", "x", "x", "x" }));

    [Fact]
    public async Task CatalogRequiresAuthenticationAndAntiforgery()
    {
        await using var factory = Factory();
        using var client = Client(factory);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/Catalog/Versions")).StatusCode);
        await Login(client, "admin@example.test", "Test-Admin-123!");
        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/Catalog/Imports", new { seasonName = "2026/27", csv = CatalogCsv() })).StatusCode);
    }

    [Fact]
    public async Task CatalogBoundaryRejectsMissingFieldsAndOversizedInput()
    {
        await using var factory = Factory();
        using var admin = Client(factory);
        await Login(admin, "admin@example.test", "Test-Admin-123!");
        foreach (var payload in new[]
        {
            new { seasonName = (string?)null, csv = (string?)CatalogCsv() },
            new { seasonName = (string?)"2026/27", csv = (string?)null },
            new { seasonName = (string?)"2026/27", csv = (string?)new string('x', 1_048_577) }
        })
        {
            var result = await admin.PostAsJsonAsync("/api/Catalog/Imports", payload);
            Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
            var body = await result.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(body.GetProperty("isSuccess").GetBoolean());
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/Catalog/Versions?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/Catalog/Versions?page=not-a-number")).StatusCode);
    }

    [Fact]
    public async Task CatalogImportCanBeReviewedPublishedAndSelectedByLeague()
    {
        await using var factory = Factory();
        using var admin = Client(factory);
        using var reader = Client(factory);
        await Login(admin, "admin@example.test", "Test-Admin-123!");
        var seasonName = "HTTP-" + Guid.NewGuid().ToString("N")[..12];
        var draft = await Data(await admin.PostAsJsonAsync("/api/Catalog/Imports", new { seasonName, csv = CatalogCsv() }), HttpStatusCode.Created);
        var id = draft.GetProperty("id").GetGuid();
        Assert.Equal("Draft", draft.GetProperty("status").GetString());
        Assert.Equal(2, draft.GetProperty("entryCount").GetInt32());
        var replay = await Data(await admin.PostAsJsonAsync("/api/Catalog/Imports", new { seasonName, csv = CatalogCsv() }), HttpStatusCode.Created);
        Assert.Equal(id, replay.GetProperty("id").GetGuid());
        var entries = await Data(await admin.GetAsync($"/api/Catalog/Versions/{id}/Entries?role=P&pageSize=1"), HttpStatusCode.OK);
        Assert.Equal(1, entries.GetProperty("total").GetInt32());
        Assert.Equal("Portiere Uno", entries.GetProperty("items")[0].GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync($"/api/Catalog/Versions/{id}/Entries?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/Catalog/Imports", new { seasonName, csv = "invalid" })).StatusCode);

        var readerEmail = Guid.NewGuid() + "@example.test";
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await users.CreateAsync(new ApplicationUser { UserName = readerEmail, Email = readerEmail, EmailConfirmed = true, DisplayName = "Lettore" }, Password)).Succeeded);
        }
        await Login(reader, readerEmail, Password);
        Assert.Equal(HttpStatusCode.NotFound, (await reader.GetAsync($"/api/Catalog/Versions/{id}/Entries")).StatusCode);
        var hidden = await Data(await reader.GetAsync($"/api/Catalog/Versions?seasonName={seasonName}"), HttpStatusCode.OK);
        Assert.Equal(0, hidden.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync("/api/Catalog/Imports", new { seasonName, csv = CatalogCsv() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync($"/api/Catalog/Versions/{id}/Publish", new { })).StatusCode);

        var league = await Data(await admin.PostAsJsonAsync("/api/Leagues/", new { name = "Lega Catalogo", seasonName, organizerEmail = Guid.NewGuid() + "@example.test", organizerName = "Organizer" }), HttpStatusCode.Created);
        var leagueId = league.GetProperty("id").GetGuid();
        var seasonId = league.GetProperty("leagueSeasonId").GetGuid();
        var route = $"/api/Leagues/{leagueId}/Seasons/{seasonId}/Catalog";
        Assert.Equal(JsonValueKind.Null, (await Data(await admin.GetAsync(route), HttpStatusCode.OK)).ValueKind);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync(route, new { listVersionId = id })).StatusCode);
        var published = await Data(await admin.PostAsJsonAsync($"/api/Catalog/Versions/{id}/Publish", new { }), HttpStatusCode.OK);
        Assert.Equal("Published", published.GetProperty("status").GetString());
        var visible = await Data(await reader.GetAsync($"/api/Catalog/Versions/{id}/Entries?search=Attaccante"), HttpStatusCode.OK);
        Assert.Equal(1, visible.GetProperty("total").GetInt32());
        await Data(await admin.PutAsJsonAsync(route, new { listVersionId = id }), HttpStatusCode.OK);
        Assert.Equal(id, (await Data(await admin.GetAsync(route), HttpStatusCode.OK)).GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync(route)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PutAsJsonAsync(route, new { listVersionId = id })).StatusCode);

        var schema = await admin.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        Assert.True(schema.GetProperty("paths").TryGetProperty("/api/Catalog/Imports", out _));
    }
}
