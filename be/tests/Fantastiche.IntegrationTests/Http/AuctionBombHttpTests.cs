using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Fantastiche.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fantastiche.IntegrationTests.Http;

public sealed partial class HttpFlowTests
{
    [Fact]
    public async Task BombSignalRReconnectReturnsOnlyTheWatchingTeamsOwnOffer()
    {
        await using var factory = Factory();
        using var organizer = Client(factory);
        var setup = await AuctionRealtimePrepareSession(organizer);
        using var member = Client(factory);
        var memberCookie = await AuctionRealtimeLoginCookie(member, setup.MemberEmail, Password);
        await RefreshCsrf(member);
        var organizerCookie = await AuctionRealtimeLoginCookie(organizer, "admin@example.test", "Test-Admin-123!");
        await RefreshCsrf(organizer);
        var route = $"/api/Auctions/Sessions/{setup.SessionId}";
        var catalog = await Data(await organizer.GetAsync(route + "/Catalog?role=P"), HttpStatusCode.OK);
        var playerId = catalog.GetProperty("items")[0].GetProperty("playerId").GetGuid();
        var start = await Data(await organizer.PostAsJsonAsync(route + "/Bombs", new { requestId = Guid.NewGuid(), playerId }), HttpStatusCode.OK);
        var id = start.GetProperty("auctionId").GetGuid();
        await AwaitBombCollection(organizer, route, id);
        await Data(await member.PostAsJsonAsync(route + "/BombBids", new { requestId = Guid.NewGuid(), bombAuctionId = id, round = 1, amount = 7 }), HttpStatusCode.OK);

        foreach (var (cookie, isOwner) in new[] { (organizerCookie, false), (memberCookie, true) })
        {
            using var socket = await AuctionRealtimeConnect(factory, cookie, "http://localhost:6061");
            await AuctionRealtimeSend(socket, new { type = 1, invocationId = "bomb-watch", target = "WatchSession", arguments = new[] { setup.SessionId.ToString() } });
            var completion = await AuctionRealtimeReceive(socket, message => message.GetProperty("type").GetInt32() == 3 && message.GetProperty("invocationId").GetString() == "bomb-watch");
            var bomb = completion.GetProperty("result").GetProperty("currentBomb");
            if (isOwner) Assert.Equal(7, bomb.GetProperty("ownAmount").GetInt32());
            else Assert.Equal(JsonValueKind.Null, bomb.GetProperty("ownAmount").ValueKind);
            Assert.Empty(bomb.GetProperty("revealedOffers").EnumerateArray());
            Assert.Equal(JsonValueKind.Null, bomb.GetProperty("winningAmount").ValueKind);
        }
    }

    [Theory]
    [InlineData("Bombs")]
    [InlineData("BombBids")]
    [InlineData("CancelBomb")]
    public async Task BombBoundaryRequiresAuthenticationValidPayloadAndAntiforgery(string action)
    {
        await using var factory = Factory();
        using var client = Client(factory);
        var route = $"/api/Auctions/Sessions/{Guid.NewGuid()}/{action}";
        await RefreshCsrf(client);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(route, new { })).StatusCode);
        await Login(client, "admin@example.test", "Test-Admin-123!");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(route, new { })).StatusCode);
        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(route, new
        {
            requestId = Guid.NewGuid(),
            playerId = Guid.NewGuid(),
            bombAuctionId = Guid.NewGuid(),
            round = 1,
            amount = 2
        })).StatusCode);
    }

    [Fact]
    public async Task BombHttpKeepsOtherOffersPrivateAndWorkerRevealsThenAwardsWithoutClientsDrivingIt()
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
        var session = await Data(await organizer.PostAsJsonAsync("/api/Auctions/Sessions", new
        {
            leagueId = setup.LeagueId,
            leagueSeasonId = setup.SeasonId,
            teamOrder = new[] { setup.FirstTeamId, setup.SecondTeamId }
        }), HttpStatusCode.Created);
        var sessionId = session.GetProperty("id").GetGuid();
        var route = $"/api/Auctions/Sessions/{sessionId}";
        var startRequest = new { requestId = Guid.NewGuid(), playerId };
        var start = await Data(await organizer.PostAsJsonAsync(route + "/Bombs", startRequest), HttpStatusCode.OK);
        var id = start.GetProperty("auctionId").GetGuid();
        var replay = await Data(await organizer.PostAsJsonAsync(route + "/Bombs", startRequest), HttpStatusCode.OK);
        Assert.Equal(start.GetRawText(), replay.GetRawText());
        var waiting = await Data(await organizer.GetAsync(route), HttpStatusCode.OK);
        Assert.Equal("Waiting", waiting.GetProperty("currentBomb").GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await participant.PostAsJsonAsync(route + "/BombBids", new
        {
            requestId = Guid.NewGuid(),
            bombAuctionId = id,
            round = 1,
            amount = 7
        })).StatusCode);
        await AwaitBombCollection(organizer, route, id);

        var offerRequest = new { requestId = Guid.NewGuid(), bombAuctionId = id, round = 1, amount = 7 };
        var receipt = await Data(await participant.PostAsJsonAsync(route + "/BombBids", offerRequest), HttpStatusCode.OK);
        var recovered = await Data(await participant.GetAsync(route + $"/Commands/{offerRequest.requestId}"), HttpStatusCode.OK);
        Assert.Equal(receipt.GetRawText(), recovered.GetRawText());
        var ownState = await Data(await participant.GetAsync(route), HttpStatusCode.OK);
        Assert.Equal(7, ownState.GetProperty("currentBomb").GetProperty("ownAmount").GetInt32());
        var otherState = await Data(await organizer.GetAsync(route), HttpStatusCode.OK);
        var hidden = otherState.GetProperty("currentBomb");
        Assert.Equal(JsonValueKind.Null, hidden.GetProperty("ownAmount").ValueKind);
        Assert.Equal(JsonValueKind.Null, hidden.GetProperty("winningAmount").ValueKind);
        Assert.Equal(0, hidden.GetProperty("revealedOffers").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, otherState.GetProperty("currentAuction").ValueKind);
        Assert.Equal(HttpStatusCode.Forbidden, (await participant.PostAsJsonAsync(route + "/CancelBomb", new { requestId = Guid.NewGuid(), bombAuctionId = id })).StatusCode);
        await Data(await organizer.PostAsJsonAsync(route + "/BombBids", new { requestId = Guid.NewGuid(), bombAuctionId = id, round = 1, amount = 2 }), HttpStatusCode.OK);

        // Nessun endpoint di avanzamento: il worker deve arrivare al risultato autonomamente.
        JsonElement state;
        // Due offerte: 30 secondi di attesa e 6 secondi per ciascuna, più margine per il worker.
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
        {
            while (true)
            {
                state = await Data(await organizer.GetAsync(route, timeout.Token), HttpStatusCode.OK);
                var bomb = state.GetProperty("currentBomb");
                if (bomb.GetProperty("status").GetString() == "Completed") break;
                Assert.Equal(JsonValueKind.Null, bomb.GetProperty("winningTeamId").ValueKind);
                var revealed = bomb.GetProperty("revealedOffers").EnumerateArray().Select(x => x.GetProperty("amount").GetInt32()).ToArray();
                Assert.Equal(revealed.Order(), revealed);
                await Task.Delay(100, timeout.Token);
            }
        }
        Assert.Equal(setup.SecondTeamId, state.GetProperty("currentBomb").GetProperty("winningTeamId").GetGuid());
        var auctionId = state.GetProperty("currentAuction").GetProperty("id").GetGuid();
        var roster = await Data(await participant.GetAsync(route + $"/Roster?teamId={setup.SecondTeamId}"), HttpStatusCode.OK);
        Assert.Equal(7, Assert.Single(roster.GetProperty("items").EnumerateArray()).GetProperty("price").GetInt32());
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FantasticheDbContext>();
        Assert.Equal(1, await db.Database.GetDbConnection().ExecuteScalarAsync<int>("SELECT COUNT(*) FROM BudgetMovements WHERE PlayerAuctionId = @Id", new { Id = auctionId }));
    }
    [Fact]
    public async Task BombSignalRReconnectPreservesWaitingDeadlineWithoutOpeningCollection()
    {
        await using var factory = Factory();
        using var organizer = Client(factory);
        var setup = await AuctionRealtimePrepareSession(organizer);
        var cookie = await AuctionRealtimeLoginCookie(organizer, "admin@example.test", "Test-Admin-123!");
        await RefreshCsrf(organizer);
        var route = $"/api/Auctions/Sessions/{setup.SessionId}";
        var catalog = await Data(await organizer.GetAsync(route + "/Catalog?role=P"), HttpStatusCode.OK);
        var playerId = catalog.GetProperty("items")[0].GetProperty("playerId").GetGuid();
        await Data(await organizer.PostAsJsonAsync(route + "/Bombs", new { requestId = Guid.NewGuid(), playerId }), HttpStatusCode.OK);
        var initial = await Data(await organizer.GetAsync(route), HttpStatusCode.OK);
        var deadline = initial.GetProperty("currentBomb").GetProperty("deadline").GetDateTimeOffset();
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var socket = await AuctionRealtimeConnect(factory, cookie, "http://localhost:6061");
            await AuctionRealtimeSend(socket, new { type = 1, invocationId = "waiting-watch", target = "WatchSession", arguments = new[] { setup.SessionId.ToString() } });
            var completion = await AuctionRealtimeReceive(socket, message => message.GetProperty("type").GetInt32() == 3 && message.GetProperty("invocationId").GetString() == "waiting-watch");
            var bomb = completion.GetProperty("result").GetProperty("currentBomb");
            Assert.Equal("Waiting", bomb.GetProperty("status").GetString());
            Assert.Equal(deadline, bomb.GetProperty("deadline").GetDateTimeOffset());
            Assert.Empty(bomb.GetProperty("revealedOffers").EnumerateArray());
        }
    }

    private async Task AwaitBombCollection(HttpClient client, string route, Guid bombId)
    {
        // Accelera solo la scadenza nel database temporaneo; la transizione resta del worker reale.
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FantasticheDbContext>();
            await db.Database.GetDbConnection().ExecuteAsync("UPDATE BombAuctions SET Deadline = DATEADD(second, -1, TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')) WHERE Id = @bombId AND Status = -1", new { bombId });
        }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            var state = await Data(await client.GetAsync(route, timeout.Token), HttpStatusCode.OK);
            if (state.GetProperty("currentBomb").GetProperty("status").GetString() == "Collecting") return;
            await Task.Delay(100, timeout.Token);
        }
    }

}
