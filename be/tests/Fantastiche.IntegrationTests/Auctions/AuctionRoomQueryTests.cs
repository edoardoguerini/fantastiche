using Fantastiche.Core.Auth;
using Fantastiche.Core.Exceptions;
using Fantastiche.Infrastructure.Auctions;
using Fantastiche.Infrastructure.Common.Authentication;
using Fantastiche.Infrastructure.Common.Persistence;
using Fantastiche.Infrastructure.Leagues;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fantastiche.IntegrationTests.Auctions;

public sealed partial class AuctionQueryTests
{
    [Fact]
    public async Task RoomSeparatesPersonalTeamFromManagementAndUsesLatestSessionOrSeasonCatalog()
    {
        var s = await AqCreateScenario();
        var participant = await Room(s.FirstUser);
        Assert.Equal(s.FirstTeamId, participant.MyTeamId);
        Assert.False(participant.CanManage);
        Assert.Equal(s.ActiveSessionId, participant.SessionId);
        Assert.Equal(s.CurrentListVersionId, participant.ListVersionId);
        Assert.Equal(497, Assert.Single(participant.Teams, x => x.Id == s.FirstTeamId).Budget);
        Assert.Equal(1, Assert.Single(participant.Teams, x => x.Id == s.FirstTeamId).Goalkeepers);
        var admin = await Room(fixture.Admin);
        Assert.Null(admin.MyTeamId);
        Assert.True(admin.CanManage);

        await AqRun(async services =>
        {
            var db = services.GetRequiredService<FantasticheDbContext>();
            await db.LeagueMembers.Where(x => x.LeagueId == s.LeagueId && x.UserId == s.FirstUser.UserId)
                .ExecuteUpdateAsync(x => x.SetProperty(m => m.IsOrganizer, true));
            await db.AuctionSessions.Where(x => x.Id == s.ActiveSessionId)
                .ExecuteUpdateAsync(x => x.SetProperty(a => a.Status, AuctionSessionStatus.Completed));
        });
        var organizerPlayer = await Room(s.FirstUser);
        Assert.True(organizerPlayer.CanManage);
        Assert.Equal(s.FirstTeamId, organizerPlayer.MyTeamId);
        Assert.Equal(s.ActiveSessionId, organizerPlayer.SessionId);
        var empty = await AqQuery<GetAuctionRoomQuery, AuctionRoomView>(new(s.FirstUser, s.LeagueId, s.EmptySeasonId));
        Assert.Null(empty.MyTeamId);
        Assert.Null(empty.SessionId);
        Assert.Empty(empty.Teams);
        Assert.Equal(s.CurrentListVersionId, empty.ListVersionId);

        Task<AuctionRoomView> Room(RequestContext user) => AqQuery<GetAuctionRoomQuery, AuctionRoomView>(new(user, s.LeagueId, s.SeasonId));
    }

    [Fact]
    public async Task RoomAndCatalogDenyOutsiderPendingAndRevokedAdminAndHideInactiveTeams()
    {
        var s = await AqCreateScenario();
        var outsider = await AqCreateUser("outsider");
        var revoked = await AqCreateUser("revoked");
        await AqRun(async services =>
        {
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(revoked.UserId.ToString()!))!;
            Assert.True((await users.AddToRoleAsync(user, "SuperAdmin")).Succeeded);
            Assert.True((await users.RemoveFromRoleAsync(user, "SuperAdmin")).Succeeded);
        });
        foreach (var denied in new[] { outsider, s.PendingUser, revoked with { IsSuperAdmin = true } })
        {
            var room = await Assert.ThrowsAsync<DomainException>(() => AqQuery<GetAuctionRoomQuery, AuctionRoomView>(new(denied, s.LeagueId, s.SeasonId)));
            var catalog = await Assert.ThrowsAsync<DomainException>(() => AqQuery<GetAuctionCatalogQuery, AuctionPage<AuctionCatalogPlayerView>>(new(denied, s.ActiveSessionId)));
            Assert.Equal(403, room.StatusCode);
            Assert.Equal(403, catalog.StatusCode);
        }
        await AqRun(async services => await services.GetRequiredService<FantasticheDbContext>().LeagueMembers
            .Where(x => x.LeagueId == s.LeagueId && x.UserId == s.SecondUser.UserId)
            .ExecuteUpdateAsync(x => x.SetProperty(m => m.Status, MembershipStatus.Pending)));
        var active = await AqQuery<GetAuctionRoomQuery, AuctionRoomView>(new(s.FirstUser, s.LeagueId, s.SeasonId));
        Assert.Equal(s.FirstTeamId, Assert.Single(active.Teams).Id);
        var wrongSeason = await Assert.ThrowsAsync<DomainException>(() => AqQuery<GetAuctionRoomQuery, AuctionRoomView>(new(s.FirstUser, Guid.NewGuid(), s.SeasonId)));
        Assert.Equal(404, wrongSeason.StatusCode);
    }

    [Fact]
    public async Task CatalogExcludesAllSeasonPurchasesAndReturnsBuyerWithSessionNamesWhenRequested()
    {
        var s = await AqCreateScenario();
        var available = await AqQuery<GetAuctionCatalogQuery, AuctionPage<AuctionCatalogPlayerView>>(new(s.FirstUser, s.ActiveSessionId));
        Assert.Empty(available.Items);
        Assert.Equal(0, available.Total);
        var all = await AqQuery<GetAuctionCatalogQuery, AuctionPage<AuctionCatalogPlayerView>>(new(s.FirstUser, s.ActiveSessionId, AvailableOnly: false, PageSize: 1));
        Assert.Equal(2, all.Total);
        Assert.Equal(s.SecondTeamId, Assert.Single(all.Items).TeamId);
        Assert.False(all.Items[0].IsAvailable);
        var filtered = await AqQuery<GetAuctionCatalogQuery, AuctionPage<AuctionCatalogPlayerView>>(new(s.FirstUser, s.ActiveSessionId, Search: " rinominato ", Role: "p", AvailableOnly: false));
        Assert.Equal("Portiere rinominato", Assert.Single(filtered.Items).Name);
        Assert.Equal(s.FirstTeamId, filtered.Items[0].TeamId);
        var second = await AqQuery<GetAuctionCatalogQuery, AuctionPage<AuctionCatalogPlayerView>>(new(s.FirstUser, s.ActiveSessionId, AvailableOnly: false, Page: 2, PageSize: 1));
        Assert.Equal(filtered.Items[0], Assert.Single(second.Items));
        var literal = await AqQuery<GetAuctionCatalogQuery, AuctionPage<AuctionCatalogPlayerView>>(new(s.FirstUser, s.ActiveSessionId, Search: "%", AvailableOnly: false));
        Assert.Empty(literal.Items);
    }
}
