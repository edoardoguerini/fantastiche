using Dapper;
using Fantastiche.Infrastructure.Auctions;
using Microsoft.Data.SqlClient;

namespace Fantastiche.IntegrationTests.Auctions;

public sealed partial class AuctionEngineTests
{
    [Fact]
    public async Task OpeningBidClosingAndReplayAreDurableAndAtomic()
    {
        var data = await Seed();
        var session = await Create(data);
        Assert.Equal(1, session.Version);
        var start = await Start(data, session.Id);
        Assert.True(start.Accepted);
        Assert.Equal(2, start.Version);
        var opened = await State(data, session.Id);
        Assert.Equal(1, opened.CurrentAuction!.CurrentAmount);
        Assert.Equal(start.ServerTime.AddSeconds(30), opened.CurrentAuction.Deadline);
        Assert.Equal(new[] { 1, 5 }, opened.CurrentAuction.Increments);
        var bidCommand = new PlaceBidCommand(data.Users[1], session.Id, Guid.NewGuid(), start.AuctionId!.Value, 7);
        var bid = await Send<PlaceBidCommand, AuctionCommandResult>(bidCommand);
        Assert.True(bid.Accepted);
        Assert.Equal(3, bid.Version);
        Assert.Equal(bid.ServerTime.AddSeconds(30), (await State(data, session.Id)).CurrentAuction!.Deadline);
        Assert.False(await Close(start.AuctionId.Value));
        await Expire(start.AuctionId.Value);
        Assert.True(await Close(start.AuctionId.Value));
        Assert.False(await Close(start.AuctionId.Value));
        Assert.Equal(bid, await Send<PlaceBidCommand, AuctionCommandResult>(bidCommand));
        var state = await State(data, session.Id);
        Assert.Equal(4, state.Version);
        Assert.Equal(data.Teams[0], state.CurrentTeamId);
        Assert.Equal("Closed", state.CurrentAuction!.Status);
        Assert.Equal(3, state.Teams.Single(x => x.Id == data.Teams[1]).Budget);
        await using var sql = new SqlConnection(fixture.ConnectionString);
        Assert.Equal(1, await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM RosterEntries WHERE PlayerAuctionId = @Id", new { Id = start.AuctionId }));
        Assert.Equal(-7, await sql.ExecuteScalarAsync<int>("SELECT SUM(Amount) FROM BudgetMovements WHERE PlayerAuctionId = @Id", new { Id = start.AuctionId }));
    }

    [Fact]
    public async Task RejectedBidReceiptPreservesTimerVersionAndOriginalPayload()
    {
        var data = await Seed();
        var session = await Create(data);
        var start = await Start(data, session.Id);
        var before = await State(data, session.Id);
        var request = new PlaceBidCommand(data.Users[1], session.Id, Guid.NewGuid(), start.AuctionId!.Value, 10);
        var rejected = await Send<PlaceBidCommand, AuctionCommandResult>(request);
        Assert.False(rejected.Accepted);
        Assert.Equal("auction.insufficient_budget", rejected.ErrorCode);
        var changed = await Send<PlaceBidCommand, AuctionCommandResult>(request with { Amount = 2 });
        Assert.Equal(409, changed.StatusCode);
        Assert.Equal("auction.request_conflict", changed.ErrorCode);
        var after = await State(data, session.Id);
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(before.CurrentAuction!.Deadline, after.CurrentAuction!.Deadline);
        Assert.Equal(rejected, await Send<PlaceBidCommand, AuctionCommandResult>(request));
        Assert.Equal(rejected, await Send<GetAuctionReceiptQuery, AuctionCommandResult>(new(data.Users[1], session.Id, request.RequestId)));
    }
}
