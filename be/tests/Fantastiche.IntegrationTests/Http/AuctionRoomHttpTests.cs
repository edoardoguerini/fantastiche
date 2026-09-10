using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fantastiche.Infrastructure.Common.Authentication;
using Fantastiche.Infrastructure.Common.Persistence;
using Fantastiche.Infrastructure.Leagues;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fantastiche.IntegrationTests.Http;

public sealed partial class HttpFlowTests
{
    [Fact]
    public async Task AuctionRoomHttpExposesPersonalContextAndSessionCatalogWithValidatedFilters()
    {
        await using var factory = Factory();
        using var admin = Client(factory);
        using var participant = Client(factory);
        using var anonymous = Client(factory);
        await Login(admin, "admin@example.test", "Test-Admin-123!");
        var setup = await PrepareAuctionLeague();
        await Login(participant, setup.Email, Password);
        var roomRoute = $"/api/Leagues/{setup.LeagueId}/Seasons/{setup.SeasonId}/AuctionRoom";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(roomRoute)).StatusCode);
        var empty = await Data(await participant.GetAsync(roomRoute), HttpStatusCode.OK);
        Assert.Equal(setup.SecondTeamId, empty.GetProperty("myTeamId").GetGuid());
        Assert.False(empty.GetProperty("canManage").GetBoolean());
        Assert.Equal(JsonValueKind.Null, empty.GetProperty("sessionId").ValueKind);
        Assert.Equal(JsonValueKind.Null, empty.GetProperty("listVersionId").ValueKind);
        Assert.Equal(2, empty.GetProperty("teams").GetArrayLength());

        var draft = await Data(await admin.PostAsJsonAsync("/api/Catalog/Imports", new { seasonName = setup.SeasonName, csv = CatalogCsv() }), HttpStatusCode.Created);
        var listId = draft.GetProperty("id").GetGuid();
        await Data(await admin.PostAsJsonAsync($"/api/Catalog/Versions/{listId}/Publish", new { }), HttpStatusCode.OK);
        await Data(await admin.PutAsJsonAsync($"/api/Leagues/{setup.LeagueId}/Seasons/{setup.SeasonId}/Catalog", new { listVersionId = listId }), HttpStatusCode.OK);
        var session = await Data(await admin.PostAsJsonAsync("/api/Auctions/Sessions", new { leagueId = setup.LeagueId, leagueSeasonId = setup.SeasonId, teamOrder = new[] { setup.FirstTeamId, setup.SecondTeamId } }), HttpStatusCode.Created);
        var sessionId = session.GetProperty("id").GetGuid();
        var room = await Data(await admin.GetAsync(roomRoute), HttpStatusCode.OK);
        Assert.True(room.GetProperty("canManage").GetBoolean());
        Assert.Equal(setup.FirstTeamId, room.GetProperty("myTeamId").GetGuid());
        Assert.Equal(sessionId, room.GetProperty("sessionId").GetGuid());
        Assert.Equal(listId, room.GetProperty("listVersionId").GetGuid());
        var catalogRoute = $"/api/Auctions/Sessions/{sessionId}/Catalog";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(catalogRoute)).StatusCode);
        var catalog = await Data(await participant.GetAsync(catalogRoute + "?role=P&pageSize=1"), HttpStatusCode.OK);
        Assert.Equal(1, catalog.GetProperty("items").GetArrayLength());
        Assert.Equal("P", catalog.GetProperty("items")[0].GetProperty("role").GetString());
        Assert.True(catalog.GetProperty("items")[0].GetProperty("isAvailable").GetBoolean());
        Assert.Equal(JsonValueKind.Null, catalog.GetProperty("items")[0].GetProperty("photoUrl").ValueKind);
        Assert.Equal(JsonValueKind.Null, catalog.GetProperty("items")[0].GetProperty("clubLogoUrl").ValueKind);
        Assert.Equal(JsonValueKind.Null, catalog.GetProperty("items")[0].GetProperty("teamId").ValueKind);
        foreach (var query in new[] { "page=0", "pageSize=101", "role=X", "search=" + new string('a', 201) })
            Assert.Equal(HttpStatusCode.BadRequest, (await participant.GetAsync(catalogRoute + "?" + query)).StatusCode);

        var outsideSetup = await PrepareAuctionLeague();
        using var outsider = Client(factory);
        await Login(outsider, outsideSetup.Email, Password);
        await Denied(outsider);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FantasticheDbContext>();
            await db.LeagueMembers.Where(x => x.LeagueId == setup.LeagueId && x.UserId != fixture.Admin.UserId)
                .ExecuteUpdateAsync(x => x.SetProperty(m => m.Status, MembershipStatus.Pending));
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await users.AddToRoleAsync((await users.FindByEmailAsync(outsideSetup.Email))!, "SuperAdmin")).Succeeded);
        }
        await Denied(participant);
        using var staleAdmin = Client(factory);
        await Login(staleAdmin, outsideSetup.Email, Password);
        await Data(await staleAdmin.GetAsync(roomRoute), HttpStatusCode.OK);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await users.RemoveFromRoleAsync((await users.FindByEmailAsync(outsideSetup.Email))!, "SuperAdmin")).Succeeded);
        }
        await Denied(staleAdmin);

        async Task Denied(HttpClient client)
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(roomRoute)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(catalogRoute)).StatusCode);
        }
    }
}
