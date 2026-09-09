using Dapper;
using Fantastiche.Infrastructure.Auctions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Fantastiche.IntegrationTests.Auctions;

public sealed partial class AuctionEngineTests
{
    [Fact]
    public async Task FailedConditionalDebitRollsBackPurchaseAndWorkerContinuesOtherSeasons()
    {
        var broken = await Seed();
        var healthy = await Seed();
        var brokenSession = await Create(broken);
        var healthySession = await Create(healthy);
        var brokenStart = await Start(broken, brokenSession.Id);
        var healthyStart = await Start(healthy, healthySession.Id);
        await Expire(brokenStart.AuctionId!.Value);
        await Expire(healthyStart.AuctionId!.Value);
        await Execute("UPDATE Teams SET Budget = 0 WHERE Id = @Id", new { Id = broken.Teams[0] });
        await using var scope = fixture.Services.CreateAsyncScope();
        var engine = scope.ServiceProvider.GetRequiredService<AuctionEngine>();
        Assert.Equal(1, await engine.CloseExpiredAsync(default));
        Assert.Equal("Closed", (await State(healthy, healthySession.Id)).CurrentAuction!.Status);
        var unchanged = await State(broken, brokenSession.Id);
        Assert.Equal("Open", unchanged.CurrentAuction!.Status);
        Assert.Equal(brokenStart.Version, unchanged.Version);
        await using var sql = new SqlConnection(fixture.ConnectionString);
        Assert.Equal(0, await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM BudgetMovements WHERE PlayerAuctionId = @Id", new { Id = brokenStart.AuctionId }));
        Assert.Equal(0, await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM RosterEntries WHERE PlayerAuctionId = @Id", new { Id = brokenStart.AuctionId }));
        await Execute("UPDATE Teams SET Budget = 10 WHERE Id = @Id", new { Id = broken.Teams[0] });
        Assert.Equal(1, await engine.CloseExpiredAsync(default));
    }

    [Fact]
    public async Task CompletedRostersAreSkippedWhenTheCallerLosesTheAuction()
    {
        var data = await Seed(goalkeepers: 1, defenders: 0);
        var session = await Create(data);
        var first = await Start(data, session.Id);
        Assert.True((await Send<PlaceBidCommand, AuctionCommandResult>(new(data.Users[1], session.Id, Guid.NewGuid(), first.AuctionId!.Value, 2))).Accepted);
        await Expire(first.AuctionId.Value);
        await Close(first.AuctionId.Value);
        Assert.Equal(data.Teams[0], (await State(data, session.Id)).CurrentTeamId);
        var second = await Start(data, session.Id, player: 1);
        Assert.True(second.Accepted);
        Assert.Equal("auction.roster_full", (await Send<PlaceBidCommand, AuctionCommandResult>(new(data.Users[1], session.Id, Guid.NewGuid(), second.AuctionId!.Value, 3))).ErrorCode);
        await Expire(second.AuctionId.Value);
        await Close(second.AuctionId.Value);
        Assert.Equal("Completed", (await State(data, session.Id)).Status);
    }
    [Fact]
    public async Task WorkerPagesPastFiftyBrokenAuctionsToReachOtherDueSeasons()
    {
        var broken = new List<(Scenario Data, Guid AuctionId)>();
        for (var i = 0; i < 51; i++)
        {
            var data = await Seed(teamCount: 1);
            var session = await Create(data);
            var started = await Start(data, session.Id);
            await Expire(started.AuctionId!.Value);
            await Execute("UPDATE Teams SET Budget = 0 WHERE Id = @Id", new { Id = data.Teams[0] });
            broken.Add((data, started.AuctionId.Value));
        }
        var healthy = await Seed(teamCount: 1);
        var healthySession = await Create(healthy);
        var healthyStart = await Start(healthy, healthySession.Id);
        await Expire(healthyStart.AuctionId!.Value);
        await using var scope = fixture.Services.CreateAsyncScope();
        var engine = scope.ServiceProvider.GetRequiredService<AuctionEngine>();
        try
        {
            Assert.Equal(1, await engine.CloseExpiredAsync(default));
            Assert.Equal("Closed", (await State(healthy, healthySession.Id)).CurrentAuction!.Status);
        }
        finally
        {
            foreach (var item in broken)
            {
                await Execute("UPDATE Teams SET Budget = 10 WHERE Id = @Id", new { Id = item.Data.Teams[0] });
                await Close(item.AuctionId);
            }
        }
    }
    [Fact]
    public async Task WorkerStopsAfterFiftySuccessfulClosuresAndHonorsCancellation()
    {
        for (var i = 0; i < 51; i++)
        {
            var data = await Seed(teamCount: 1);
            var session = await Create(data);
            var started = await Start(data, session.Id);
            await Expire(started.AuctionId!.Value);
        }
        await using var scope = fixture.Services.CreateAsyncScope();
        var engine = scope.ServiceProvider.GetRequiredService<AuctionEngine>();
        Assert.Equal(50, await engine.CloseExpiredAsync(default));
        Assert.Equal(1, await engine.CloseExpiredAsync(default));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => engine.CloseExpiredAsync(new CancellationToken(true)));
    }
}
