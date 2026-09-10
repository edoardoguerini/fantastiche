using System.Net;
using Fantastiche.Infrastructure.Common.Authentication;
using Fantastiche.Infrastructure.Common.Persistence;
using Fantastiche.Infrastructure.Leagues;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Fantastiche.IntegrationTests.Http;

public sealed partial class HttpFlowTests
{
    [Fact]
    public async Task LeagueListRequiresAuthentication()
    {
        await using var factory = Factory();
        using var client = Client(factory);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/Leagues")).StatusCode);
    }

    [Fact]
    public async Task LeagueListReturnsOnlyActiveMembershipsAndKeepsLeagueDataIsolated()
    {
        var user = await CreateLeagueListUser();
        var active = await AddLeagueForList("Attiva", "2026/27", user.Id, MembershipStatus.Active, new DateTimeOffset(2026, 9, 3, 0, 0, 0, TimeSpan.Zero));
        await AddLeagueForList("In attesa", "2026/27", user.Id, MembershipStatus.Pending, new DateTimeOffset(2026, 9, 2, 0, 0, 0, TimeSpan.Zero));
        await AddLeagueForList("Altra lega", "2026/27", null, null, new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));
        await using var factory = Factory();
        using var client = Client(factory);
        await Login(client, user.Email, Password);

        var data = await Data(await client.GetAsync("/api/Leagues"), HttpStatusCode.OK);

        Assert.Equal(1, data.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, data.GetProperty("page").GetInt32());
        Assert.Equal(20, data.GetProperty("pageSize").GetInt32());
        var item = Assert.Single(data.GetProperty("items").EnumerateArray());
        Assert.Equal(active.LeagueId, item.GetProperty("id").GetGuid());
        Assert.Equal("Attiva", item.GetProperty("name").GetString());
        Assert.Equal(active.SeasonId, item.GetProperty("leagueSeasonId").GetGuid());
        Assert.Equal("2026/27", item.GetProperty("seasonName").GetString());
        Assert.Equal(700, item.GetProperty("budget").GetInt32());
        Assert.Equal(4, item.GetProperty("goalkeepers").GetInt32());
        Assert.Equal(9, item.GetProperty("defenders").GetInt32());
        Assert.Equal(8, item.GetProperty("midfielders").GetInt32());
        Assert.Equal(7, item.GetProperty("forwards").GetInt32());
    }

    [Fact]
    public async Task LeagueListLetsDatabaseSuperAdminReadAllLeagues()
    {
        var pendingUser = await CreateLeagueListUser();
        var pending = await AddLeagueForList("Visibile admin", "2027/28", pendingUser.Id, MembershipStatus.Pending, DateTimeOffset.UtcNow);
        await using var factory = Factory();
        using var admin = Client(factory);
        await Login(admin, "admin@example.test", "Test-Admin-123!");

        var data = await Data(await admin.GetAsync("/api/Leagues?pageSize=100"), HttpStatusCode.OK);

        Assert.Contains(data.GetProperty("items").EnumerateArray(), item =>
            item.GetProperty("id").GetGuid() == pending.LeagueId &&
            item.GetProperty("leagueSeasonId").GetGuid() == pending.SeasonId);
    }

    [Fact]
    public async Task LeagueListDoesNotTrustAStaleSuperAdminCookieAfterDatabaseRoleRemoval()
    {
        var otherUser = await CreateLeagueListUser();
        var hidden = await AddLeagueForList("Ruolo revocato", "2027/28", otherUser.Id, MembershipStatus.Active, DateTimeOffset.UtcNow);
        await using var factory = Factory();
        using var admin = Client(factory);
        await Login(admin, "admin@example.test", "Test-Admin-123!");
        await SetAdminRole(false);

        try
        {
            var data = await Data(await admin.GetAsync("/api/Leagues?pageSize=100"), HttpStatusCode.OK);

            Assert.DoesNotContain(data.GetProperty("items").EnumerateArray(), item =>
                item.GetProperty("id").GetGuid() == hidden.LeagueId);
        }
        finally
        {
            await SetAdminRole(true);
        }
    }

    [Fact]
    public async Task LeagueListPaginatesWithStableNewestFirstOrdering()
    {
        var user = await CreateLeagueListUser();
        var oldest = await AddLeagueForList("Prima", "2026/27", user.Id, MembershipStatus.Active, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var middle = await AddLeagueForList("Seconda", "2026/27", user.Id, MembershipStatus.Active, new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));
        var newest = await AddLeagueForList("Terza", "2026/27", user.Id, MembershipStatus.Active, new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));
        await using var factory = Factory();
        using var client = Client(factory);
        await Login(client, user.Email, Password);

        var firstPage = await Data(await client.GetAsync("/api/Leagues?page=1&pageSize=2"), HttpStatusCode.OK);
        var secondPage = await Data(await client.GetAsync("/api/Leagues?page=2&pageSize=2"), HttpStatusCode.OK);

        Assert.Equal(3, firstPage.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, firstPage.GetProperty("page").GetInt32());
        Assert.Equal(2, firstPage.GetProperty("pageSize").GetInt32());
        Assert.Equal(new[] { newest.LeagueId, middle.LeagueId }, firstPage.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()));
        Assert.Equal(3, secondPage.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, secondPage.GetProperty("page").GetInt32());
        Assert.Equal(2, secondPage.GetProperty("pageSize").GetInt32());
        Assert.Equal(oldest.LeagueId, Assert.Single(secondPage.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task LeagueListReturnsOnlyTheCurrentSeasonSelectedByLeagueDetail()
    {
        var user = await CreateLeagueListUser();
        var league = await AddLeagueForList("Due stagioni", "2025/26", user.Id, MembershipStatus.Active, DateTimeOffset.UtcNow);
        var currentSeasonId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        await AddSeasonForList(league.LeagueId, currentSeasonId, "2026/27");
        await using var factory = Factory();
        using var client = Client(factory);
        await Login(client, user.Email, Password);

        var list = await Data(await client.GetAsync("/api/Leagues"), HttpStatusCode.OK);
        var detail = await Data(await client.GetAsync($"/api/Leagues/{league.LeagueId}"), HttpStatusCode.OK);

        Assert.Equal(1, list.GetProperty("totalCount").GetInt32());
        var item = Assert.Single(list.GetProperty("items").EnumerateArray());
        Assert.Equal(currentSeasonId, item.GetProperty("leagueSeasonId").GetGuid());
        Assert.Equal(detail.GetProperty("leagueSeasonId").GetGuid(), item.GetProperty("leagueSeasonId").GetGuid());
        Assert.Equal("2026/27", item.GetProperty("seasonName").GetString());
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=not-a-number")]
    public async Task LeagueListRejectsInvalidPaging(string query)
    {
        await using var factory = Factory();
        using var admin = Client(factory);
        await Login(admin, "admin@example.test", "Test-Admin-123!");

        var response = await admin.GetAsync("/api/Leagues?" + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<(Guid Id, string Email)> CreateLeagueListUser()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = Guid.NewGuid().ToString("N") + "@example.test";
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Utente elenco"
        };
        var result = await manager.CreateAsync(user, Password);
        Assert.True(result.Succeeded, string.Join(",", result.Errors.Select(error => error.Code)));
        return (user.Id, email);
    }

    private async Task<(Guid LeagueId, Guid SeasonId)> AddLeagueForList(
        string name,
        string seasonName,
        Guid? userId,
        MembershipStatus? membershipStatus,
        DateTimeOffset createdAt)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FantasticheDbContext>();
        var league = new League { Name = name, CreatedAt = createdAt };
        var season = new LeagueSeason
        {
            LeagueId = league.Id,
            Name = seasonName,
            Budget = 700,
            Goalkeepers = 4,
            Defenders = 9,
            Midfielders = 8,
            Forwards = 7
        };
        db.AddRange(league, season);
        if (userId is not null && membershipStatus is not null)
        {
            db.LeagueMembers.Add(new LeagueMember
            {
                LeagueId = league.Id,
                UserId = userId.Value,
                Status = membershipStatus.Value
            });
        }
        await db.SaveChangesAsync();
        return (league.Id, season.Id);
    }

    private async Task SetAdminRole(bool enabled)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = (await manager.FindByEmailAsync("admin@example.test"))!;
        var result = enabled
            ? await manager.AddToRoleAsync(admin, "SuperAdmin")
            : await manager.RemoveFromRoleAsync(admin, "SuperAdmin");
        Assert.True(result.Succeeded, string.Join(",", result.Errors.Select(error => error.Code)));
    }

    private async Task AddSeasonForList(Guid leagueId, Guid seasonId, string seasonName)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FantasticheDbContext>();
        db.LeagueSeasons.Add(new LeagueSeason
        {
            Id = seasonId,
            LeagueId = leagueId,
            Name = seasonName,
            Budget = 800,
            Goalkeepers = 3,
            Defenders = 8,
            Midfielders = 8,
            Forwards = 6
        });
        await db.SaveChangesAsync();
    }
}
