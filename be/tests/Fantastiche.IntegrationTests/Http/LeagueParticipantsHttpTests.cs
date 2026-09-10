using System.Net;
using System.Net.Http.Json;
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
    public async Task ParticipantsManagementScopesPrivateInvitationsAndRejectsRevokedAdminWrites()
    {
        await using var factory = Factory();
        using var admin = Client(factory);
        using var participant = Client(factory);
        using var outsider = Client(factory);
        using var anonymous = Client(factory);
        await Login(admin, "admin@example.test", "Test-Admin-123!");
        var setup = await PrepareAuctionLeague();
        var outside = await PrepareAuctionLeague();
        await Login(participant, setup.Email, Password);
        await Login(outsider, outside.Email, Password);
        var route = $"/api/Leagues/{setup.LeagueId}/Seasons/{setup.SeasonId}/Participants";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(route)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync(route)).StatusCode);
        var readOnly = await Data(await participant.GetAsync(route), HttpStatusCode.OK);
        Assert.False(readOnly.GetProperty("canManage").GetBoolean());
        Assert.Empty(readOnly.GetProperty("invitations").GetProperty("items").EnumerateArray());
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FantasticheDbContext>();
            var account = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(setup.Email);
            await db.LeagueMembers.Where(x => x.LeagueId == setup.LeagueId && x.UserId == account!.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(m => m.IsOrganizer, true));
        }
        var organizer = await Data(await participant.GetAsync(route), HttpStatusCode.OK);
        Assert.True(organizer.GetProperty("canManage").GetBoolean());
        var invited = await Data(await admin.PostAsJsonAsync($"/api/Leagues/{setup.LeagueId}/Invitations",
            new { leagueSeasonId = setup.SeasonId, email = Guid.NewGuid() + "@example.test", displayName = "Invitato gestione" }), HttpStatusCode.Created);
        var invitationId = invited.GetProperty("id").GetGuid();
        var managed = await Data(await admin.GetAsync(route + "?pageSize=1"), HttpStatusCode.OK);
        Assert.True(managed.GetProperty("canManage").GetBoolean());
        Assert.Equal(2, managed.GetProperty("participants").GetArrayLength());
        var item = Assert.Single(managed.GetProperty("invitations").GetProperty("items").EnumerateArray());
        Assert.Equal(invitationId, item.GetProperty("id").GetGuid());
        Assert.Equal("Pending", item.GetProperty("status").GetString());
        Assert.False(item.TryGetProperty("tokenHash", out _));
        Assert.False(item.TryGetProperty("token", out _));
        var empty = await Data(await admin.GetAsync(route + "?page=2&pageSize=1"), HttpStatusCode.OK);
        Assert.Empty(empty.GetProperty("invitations").GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(route + "?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/Leagues/{outside.LeagueId}/Seasons/{setup.SeasonId}/Participants")).StatusCode);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await users.AddToRoleAsync((await users.FindByEmailAsync(outside.Email))!, "SuperAdmin")).Succeeded);
        }
        using var staleAdmin = Client(factory);
        await Login(staleAdmin, outside.Email, Password);
        await Data(await staleAdmin.GetAsync(route), HttpStatusCode.OK);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await users.RemoveFromRoleAsync((await users.FindByEmailAsync(outside.Email))!, "SuperAdmin")).Succeeded);
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await staleAdmin.GetAsync(route)).StatusCode);
        foreach (var action in new[] { "Revoke", "Resend" })
            Assert.Equal(HttpStatusCode.Forbidden, (await staleAdmin.PostAsJsonAsync($"/api/Leagues/{setup.LeagueId}/Invitations/{invitationId}/{action}", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staleAdmin.PostAsJsonAsync($"/api/Leagues/{setup.LeagueId}/Invitations",
            new { leagueSeasonId = setup.SeasonId, email = Guid.NewGuid() + "@example.test", displayName = "Vietato" })).StatusCode);
        var revoked = await Data(await admin.PostAsJsonAsync($"/api/Leagues/{setup.LeagueId}/Invitations/{invitationId}/Revoke", new { }), HttpStatusCode.OK);
        Assert.True(revoked.GetBoolean());
        var final = await Data(await admin.GetAsync(route), HttpStatusCode.OK);
        Assert.Equal("Revoked", final.GetProperty("invitations").GetProperty("items")[0].GetProperty("status").GetString());
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FantasticheDbContext>();
            var account = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(setup.Email);
            await db.LeagueMembers.Where(x => x.LeagueId == setup.LeagueId && x.UserId == account!.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(m => m.Status, MembershipStatus.Pending));
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await participant.GetAsync(route)).StatusCode);
    }
}
