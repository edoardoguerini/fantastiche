using System.Net;
using System.Net.Http.Json;
using Fantastiche.Infrastructure.Catalog;
using Fantastiche.Infrastructure.Common.Authentication;
using Fantastiche.Infrastructure.Common.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fantastiche.IntegrationTests.Http;

public sealed partial class HttpFlowTests
{
    [Fact]
    public async Task CatalogRechecksRevokedAdminBeforeWritesAndDraftOrLeagueReads()
    {
        await using var factory = Factory();
        using var client = Client(factory);
        var email = $"catalog-admin-{Guid.NewGuid():N}@example.test";
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { Email = email, UserName = email, EmailConfirmed = true, DisplayName = "Catalog admin" };
            Assert.True((await users.CreateAsync(user, Password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, "SuperAdmin")).Succeeded);
        }
        await Login(client, email, Password);
        var seasonName = "Revoked-" + Guid.NewGuid().ToString("N");
        var draft = await Data(await client.PostAsJsonAsync("/api/Catalog/Imports", new { seasonName, csv = CatalogCsv() }), HttpStatusCode.Created);
        var draftId = draft.GetProperty("id").GetGuid();
        var published = await Data(await client.PostAsJsonAsync("/api/Catalog/Imports", new
        {
            seasonName,
            csv = CatalogCsv().Replace("Portiere Uno", "Portiere nuovo", StringComparison.Ordinal)
        }), HttpStatusCode.Created);
        var publishedId = published.GetProperty("id").GetGuid();
        await Data(await client.PostAsJsonAsync($"/api/Catalog/Versions/{publishedId}/Publish", new { }), HttpStatusCode.OK);
        var league = await Data(await client.PostAsJsonAsync("/api/Leagues/", new
        {
            name = "Catalogo revoca",
            seasonName,
            organizerEmail = $"organizer-{Guid.NewGuid():N}@example.test",
            organizerName = "Organizzatore"
        }), HttpStatusCode.Created);
        var leagueId = league.GetProperty("id").GetGuid();
        var seasonId = league.GetProperty("leagueSeasonId").GetGuid();
        var leagueRoute = $"/api/Leagues/{leagueId}/Seasons/{seasonId}/Catalog";
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await users.RemoveFromRoleAsync((await users.FindByEmailAsync(email))!, "SuperAdmin")).Succeeded);
        }

        // Lo stesso cookie contiene ancora la claim SuperAdmin emessa prima della revoca.
        var visible = await Data(await client.GetAsync($"/api/Catalog/Versions?seasonName={seasonName}"), HttpStatusCode.OK);
        var statuses = new[]
        {
            ("draft entries", (await client.GetAsync($"/api/Catalog/Versions/{draftId}/Entries")).StatusCode, HttpStatusCode.NotFound),
            ("league read", (await client.GetAsync(leagueRoute)).StatusCode, HttpStatusCode.Forbidden),
            ("import", (await client.PostAsJsonAsync("/api/Catalog/Imports", new { seasonName = "Denied-" + Guid.NewGuid().ToString("N"), csv = CatalogCsv() })).StatusCode, HttpStatusCode.Forbidden),
            ("publish", (await client.PostAsJsonAsync($"/api/Catalog/Versions/{draftId}/Publish", new { })).StatusCode, HttpStatusCode.Forbidden),
            ("league selection", (await client.PutAsJsonAsync(leagueRoute, new { listVersionId = publishedId })).StatusCode, HttpStatusCode.Forbidden)
        };
        Assert.True(statuses.All(x => x.Item2 == x.Item3), string.Join("; ", statuses.Select(x => $"{x.Item1}: {x.Item2}, atteso {x.Item3}")));
        Assert.Equal(1, visible.GetProperty("total").GetInt32());
        Assert.Equal(publishedId, visible.GetProperty("items")[0].GetProperty("id").GetGuid());
        await Data(await client.GetAsync($"/api/Catalog/Versions/{publishedId}/Entries"), HttpStatusCode.OK);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FantasticheDbContext>();
            Assert.Equal(ListVersionStatus.Draft, (await db.ListVersions.SingleAsync(x => x.Id == draftId)).Status);
            Assert.Null((await db.LeagueSeasons.SingleAsync(x => x.Id == seasonId)).ListVersionId);
        }
    }
}
