using Dapper;
using Fantastiche.Infrastructure.Auctions;
using Microsoft.Data.SqlClient;

namespace Fantastiche.IntegrationTests.Auctions;

public sealed partial class AuctionEngineTests
{
    [Fact]
    public async Task EqualConcurrentBidsAndDuplicateRequestsHaveOneEffectAcrossConnections()
    {
        var data = await Seed();
        var session = await Create(data);
        var start = await Start(data, session.Id);
        var id = start.AuctionId!.Value;
        var equal = await Task.WhenAll(data.Users.Select(user => Send<PlaceBidCommand, AuctionCommandResult>(new(user, session.Id, Guid.NewGuid(), id, 3))));
        Assert.Single(equal, x => x.Accepted);
        var command = new PlaceBidCommand(data.Users[1], session.Id, Guid.NewGuid(), id, 5);
        var repeated = await Task.WhenAll(Send<PlaceBidCommand, AuctionCommandResult>(command), Send<PlaceBidCommand, AuctionCommandResult>(command));
        Assert.Equal(repeated[0], repeated[1]);
        Assert.True(repeated[0].Accepted);
        Assert.Equal(4, (await State(data, session.Id)).Version);
        await Expire(id);
        var closings = await Task.WhenAll(Close(id), Close(id), Close(id));
        Assert.Single(closings, x => x);
        await using var sql = new SqlConnection(fixture.ConnectionString);
        Assert.Equal(1, await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM BudgetMovements WHERE PlayerAuctionId = @id", new { id }));
        Assert.Equal(1, await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM RosterEntries WHERE PlayerAuctionId = @id", new { id }));
    }

    [Fact]
    public async Task DifferentConcurrentBidsEndAtTheHighestAmountAndOldDeadlineCannotCloseExtendedAuction()
    {
        var data = await Seed();
        var session = await Create(data);
        var start = await Start(data, session.Id);
        var id = start.AuctionId!.Value;
        await Execute("UPDATE PlayerAuctions SET Deadline = DATEADD(second, 10, TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')) WHERE Id = @id", new { id });
        var old = (await State(data, session.Id)).CurrentAuction!.Deadline;
        var results = await Task.WhenAll(
            Send<PlaceBidCommand, AuctionCommandResult>(new(data.Users[0], session.Id, Guid.NewGuid(), id, 3)),
            Send<PlaceBidCommand, AuctionCommandResult>(new(data.Users[1], session.Id, Guid.NewGuid(), id, 8)));
        Assert.Contains(results, x => x.Accepted);
        var current = (await State(data, session.Id)).CurrentAuction!;
        Assert.Equal(8, current.CurrentAmount);
        Assert.True(current.Deadline > old);
        Assert.False(await Close(id));
        await Expire(id);
        var lateCommand = new PlaceBidCommand(data.Users[0], session.Id, Guid.NewGuid(), id, 9);
        var late = await Send<PlaceBidCommand, AuctionCommandResult>(lateCommand);
        Assert.False(late.Accepted);
        Assert.Equal("auction.expired", late.ErrorCode);
        Assert.True(await Close(id));
        Assert.Equal(late, await Send<PlaceBidCommand, AuctionCommandResult>(lateCommand));
    }

    [Fact]
    public async Task ExpiredBidRacingClosureNeverWinsAndRecordsOnePurchase()
    {
        var data = await Seed();
        var session = await Create(data);
        var start = await Start(data, session.Id);
        var id = start.AuctionId!.Value;
        await Expire(id);
        var bid = Send<PlaceBidCommand, AuctionCommandResult>(new(data.Users[1], session.Id, Guid.NewGuid(), id, 9));
        var close = Close(id);
        await Task.WhenAll(bid, close);
        Assert.False((await bid).Accepted);
        Assert.True(await close);
        var state = await State(data, session.Id);
        Assert.Equal(data.Teams[0], state.CurrentAuction!.WinningTeamId);
        Assert.Equal(1, state.CurrentAuction.CurrentAmount);
        Assert.Equal(9, state.Teams.Single(x => x.Id == data.Teams[0]).Budget);
    }
}
