using Fantastiche.Infrastructure.Auctions;

namespace Fantastiche.IntegrationTests.Auctions;

public sealed partial class AuctionEngineTests
{
    [Fact]
    public async Task GoToTurnIsAtomicScopedAndIdempotentWithoutChangingOrder()
    {
        var data = await Seed(teamCount: 3);
        var session = await Create(data);
        var command = new ControlAuctionSessionCommand(data.Users[0], session.Id, Guid.NewGuid(), "GoToTurn", TargetTeamId: data.Teams[2]);
        var result = await Send<ControlAuctionSessionCommand, AuctionCommandResult>(command);
        Assert.True(result.Accepted);
        var state = await State(data, session.Id);
        Assert.Equal(data.Teams[2], state.CurrentTeamId);
        Assert.Equal(data.Teams, state.TeamOrder);
        Assert.Equal(result, await Send<ControlAuctionSessionCommand, AuctionCommandResult>(command));
        Assert.Equal(state.Version, (await State(data, session.Id)).Version);
        Assert.Equal("auction.request_conflict", (await Send<ControlAuctionSessionCommand, AuctionCommandResult>(command with { TargetTeamId = data.Teams[1] })).ErrorCode);
        Assert.Equal(403, (await Send<ControlAuctionSessionCommand, AuctionCommandResult>(command with { Context = data.Users[1], RequestId = Guid.NewGuid() })).StatusCode);
        Assert.False((await Send<ControlAuctionSessionCommand, AuctionCommandResult>(command with { TargetTeamId = Guid.NewGuid(), RequestId = Guid.NewGuid() })).Accepted);
        Assert.False((await Send<ControlAuctionSessionCommand, AuctionCommandResult>(command with { TargetTeamId = null, RequestId = Guid.NewGuid() })).Accepted);
        await Send<ControlAuctionSessionCommand, AuctionCommandResult>(command with { TargetTeamId = data.Teams[0], RequestId = Guid.NewGuid() });
        await Start(data, session.Id);
        Assert.Equal("auction.player_in_progress", (await Send<ControlAuctionSessionCommand, AuctionCommandResult>(command with { RequestId = Guid.NewGuid() })).ErrorCode);
    }
}
