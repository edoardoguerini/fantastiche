using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Fantastiche.Infrastructure.Common.Authentication;
using Fantastiche.Infrastructure.Common.Persistence;
using Fantastiche.Infrastructure.Leagues;
using Fantastiche.Infrastructure.Teams;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fantastiche.IntegrationTests.Http;

public sealed partial class HttpFlowTests
{
    [Fact]
    public async Task AuctionEndpointsRequireAuthentication()
    {
        await using var factory = Factory();
        using var client = Client(factory);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/Auctions/Sessions/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/Auctions/Sessions/{Guid.NewGuid()}/Roster/Export")).StatusCode);
    }

    [Fact]
    public async Task AuctionBoundaryRequiresIdentifiersAndAntiforgery()
    {
        await using var factory = Factory();
        using var client = Client(factory);
        await Login(client, "admin@example.test", "Test-Admin-123!");
        var invalid = await client.PostAsJsonAsync("/api/Auctions/Sessions", new { leagueId = Guid.Empty, leagueSeasonId = Guid.Empty, teamOrder = Array.Empty<Guid>() });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/Auctions/Sessions/{Guid.NewGuid()}/Bids", new { requestId = Guid.NewGuid(), playerAuctionId = Guid.NewGuid(), amount = 2 })).StatusCode);
    }

    [Fact]
    public async Task AuctionHttpFlowPreservesReceiptsAndWorkerAwardsOnce()
    {
        await using var factory = Factory();
        using var organizer = Client(factory);
        using var participant = Client(factory);
        await Login(organizer, "admin@example.test", "Test-Admin-123!");
        var setup = await PrepareAuctionLeague();
        await Login(participant, setup.Email, Password);
        var draft = await Data(await organizer.PostAsJsonAsync("/api/Catalog/Imports", new { seasonName = setup.SeasonName, csv = CatalogCsv() }), HttpStatusCode.Created);
        var listId = draft.GetProperty("id").GetGuid();
        await Data(await organizer.PostAsJsonAsync($"/api/Catalog/Versions/{listId}/Publish", new { }), HttpStatusCode.OK);
        await Data(await organizer.PutAsJsonAsync($"/api/Leagues/{setup.LeagueId}/Seasons/{setup.SeasonId}/Catalog", new { listVersionId = listId }), HttpStatusCode.OK);
        var entries = await Data(await organizer.GetAsync($"/api/Catalog/Versions/{listId}/Entries?role=P"), HttpStatusCode.OK);
        var playerId = entries.GetProperty("items")[0].GetProperty("playerId").GetGuid();
        var session = await Data(await organizer.PostAsJsonAsync("/api/Auctions/Sessions", new { leagueId = setup.LeagueId, leagueSeasonId = setup.SeasonId, teamOrder = new[] { setup.FirstTeamId, setup.SecondTeamId } }), HttpStatusCode.Created);
        var sessionId = session.GetProperty("id").GetGuid();
        var route = $"/api/Auctions/Sessions/{sessionId}";
        Assert.Equal(HttpStatusCode.Conflict, (await participant.GetAsync(route + "/Roster/Export")).StatusCode);
        Assert.Equal(setup.FirstTeamId, session.GetProperty("currentTeamId").GetGuid());
        var active = await Data(await participant.GetAsync($"/api/Leagues/{setup.LeagueId}/Seasons/{setup.SeasonId}/Auction"), HttpStatusCode.OK);
        Assert.Equal(sessionId, active.GetProperty("id").GetGuid());

        var wrongTurn = await participant.PostAsJsonAsync(route + "/Players", new { requestId = Guid.NewGuid(), playerId, durationSeconds = 30, increments = new[] { 1, 5, 10 } });
        Assert.Equal(HttpStatusCode.Forbidden, wrongTurn.StatusCode);
        var startId = Guid.NewGuid();
        var started = await Data(await organizer.PostAsJsonAsync(route + "/Players", new { requestId = startId, playerId, durationSeconds = 30, increments = new[] { 1, 5, 10 } }), HttpStatusCode.OK);
        var auctionId = started.GetProperty("auctionId").GetGuid();
        Assert.True(started.GetProperty("accepted").GetBoolean());
        var replay = await Data(await organizer.PostAsJsonAsync(route + "/Players", new { requestId = startId, playerId, durationSeconds = 30, increments = new[] { 1, 5, 10 } }), HttpStatusCode.OK);
        Assert.Equal(started.GetRawText(), replay.GetRawText());

        var bidId = Guid.NewGuid();
        var bid = await Data(await participant.PostAsJsonAsync(route + "/Bids", new { requestId = bidId, playerAuctionId = auctionId, amount = 5 }), HttpStatusCode.OK);
        Assert.True(bid.GetProperty("accepted").GetBoolean());
        var recovered = await Data(await participant.GetAsync(route + $"/Commands/{bidId}"), HttpStatusCode.OK);
        Assert.Equal(bid.GetRawText(), recovered.GetRawText());
        Assert.Equal(HttpStatusCode.NotFound, (await organizer.GetAsync(route + $"/Commands/{bidId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await participant.PostAsJsonAsync(route + "/Bids", new { requestId = bidId, playerAuctionId = auctionId, amount = 6 })).StatusCode);
        var rejectedId = Guid.NewGuid();
        var rejected = await organizer.PostAsJsonAsync(route + "/Bids", new { requestId = rejectedId, playerAuctionId = auctionId, amount = 5 });
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        var errorBody = await rejected.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(errorBody.GetProperty("isSuccess").GetBoolean());
        Assert.False(errorBody.GetProperty("data").GetProperty("accepted").GetBoolean());
        var recoveredRejection = await Data(await organizer.GetAsync(route + $"/Commands/{rejectedId}"), HttpStatusCode.OK);
        Assert.Equal(errorBody.GetProperty("data").GetRawText(), recoveredRejection.GetRawText());
        Assert.Equal(HttpStatusCode.Conflict, (await organizer.PostAsJsonAsync(route + "/Control", new { requestId = Guid.NewGuid(), action = "Pause" })).StatusCode);

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FantasticheDbContext>();
            await db.Database.GetDbConnection().ExecuteAsync("UPDATE PlayerAuctions SET Deadline = DATEADD(second, -1, SYSUTCDATETIME()) WHERE Id = @id", new { id = auctionId });
        }
        JsonElement state = default;
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
        {
            while (true)
            {
                state = await Data(await participant.GetAsync(route, timeout.Token), HttpStatusCode.OK);
                if (state.GetProperty("currentAuction").GetProperty("status").GetString() == "Closed") break;
                await Task.Delay(100, timeout.Token);
            }
        }
        Assert.Equal(setup.FirstTeamId, state.GetProperty("currentTeamId").GetGuid());
        var winner = state.GetProperty("teams").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == setup.SecondTeamId);
        Assert.Equal(5, winner.GetProperty("budget").GetInt32());
        Assert.Equal(1, winner.GetProperty("goalkeepers").GetInt32());
        var roster = await Data(await participant.GetAsync(route + $"/Roster?teamId={setup.SecondTeamId}"), HttpStatusCode.OK);
        Assert.Equal(1, roster.GetProperty("total").GetInt32());
        Assert.Equal(playerId, roster.GetProperty("items")[0].GetProperty("playerId").GetGuid());
        var exportResponse = await participant.GetAsync(route + "/Roster/Export");
        Assert.True(exportResponse.Headers.CacheControl!.NoStore);
        var export = await Data(exportResponse, HttpStatusCode.OK);
        Assert.StartsWith("$,$,$\nDue,", export.GetProperty("csv").GetString());
        Assert.EndsWith(",5\n", export.GetProperty("csv").GetString());
        Assert.Equal($"fantastiche-rosters-{setup.SeasonId:N}.csv", export.GetProperty("fileName").GetString());
        var bids = await Data(await participant.GetAsync(route + $"/Players/{auctionId}/Bids"), HttpStatusCode.OK);
        Assert.Equal(2, bids.GetProperty("total").GetInt32());
        await Data(await organizer.PostAsJsonAsync(route + "/Control", new { requestId = Guid.NewGuid(), action = "Complete" }), HttpStatusCode.OK);
        var final = await Data(await organizer.GetAsync(route), HttpStatusCode.OK);
        Assert.Equal("Completed", final.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, final.GetProperty("currentTeamId").ValueKind);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FantasticheDbContext>();
            Assert.Equal(1, await db.Database.GetDbConnection().QuerySingleAsync<int>("SELECT COUNT(*) FROM BudgetMovements WHERE PlayerAuctionId = @id", new { id = auctionId }));
        }
    }

    private async Task<(Guid LeagueId, Guid SeasonId, string SeasonName, Guid FirstTeamId, Guid SecondTeamId, string Email)> PrepareAuctionLeague()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FantasticheDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = Guid.NewGuid() + "@example.test";
        var user = new ApplicationUser { Email = email, UserName = email, DisplayName = "Partecipante asta", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        var league = new League { Name = "Lega Asta HTTP", CreatedAt = DateTimeOffset.UtcNow };
        var season = new LeagueSeason { LeagueId = league.Id, Name = "Auction-" + Guid.NewGuid().ToString("N")[..8], Budget = 10, Goalkeepers = 1, Defenders = 1, Midfielders = 0, Forwards = 0 };
        var first = new Team { LeagueId = league.Id, LeagueSeasonId = season.Id, Name = "Uno", NormalizedName = "UNO", Budget = 10 };
        var second = new Team { LeagueId = league.Id, LeagueSeasonId = season.Id, Name = "Due", NormalizedName = "DUE", Budget = 10 };
        db.AddRange(league, season, first, second,
            new LeagueMember { LeagueId = league.Id, UserId = fixture.Admin.UserId!.Value, Status = MembershipStatus.Active, IsOrganizer = true },
            new LeagueMember { LeagueId = league.Id, UserId = user.Id, Status = MembershipStatus.Active },
            new TeamMember { LeagueId = league.Id, LeagueSeasonId = season.Id, TeamId = first.Id, UserId = fixture.Admin.UserId.Value },
            new TeamMember { LeagueId = league.Id, LeagueSeasonId = season.Id, TeamId = second.Id, UserId = user.Id });
        await db.SaveChangesAsync();
        return (league.Id, season.Id, season.Name, first.Id, second.Id, email);
    }
}
