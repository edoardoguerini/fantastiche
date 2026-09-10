using Fantastiche.Infrastructure.Auctions;

namespace Fantastiche.IntegrationTests.Auctions;

public sealed partial class AuctionEngineTests
{
    [Fact]
    public async Task CallsMustFollowRoleOrderAndRejectedCallsHaveDurableReceipts()
    {
        var data = await Seed();
        var session = await Create(data);
        var command = new StartPlayerAuctionCommand(data.Users[0], session.Id, Guid.NewGuid(), data.Players[2], 30, [1]);
        var rejected = await Send<StartPlayerAuctionCommand, AuctionCommandResult>(command);
        Assert.False(rejected.Accepted);
        Assert.Equal("auction.wrong_role", rejected.ErrorCode);
        Assert.Equal(rejected, await Send<StartPlayerAuctionCommand, AuctionCommandResult>(command));
        Assert.Null((await State(data, session.Id)).CurrentAuction);
        Assert.Equal(1, (await State(data, session.Id)).Version);
        Assert.Equal("P", (await State(data, session.Id)).CurrentRole);
    }

    [Fact]
    public async Task WinningTeamWithFullRoleIsSkippedAndManagerCannotSelectIt()
    {
        var data = await Seed();
        var session = await Create(data);
        var first = await Start(data, session.Id);
        Assert.True((await Send<PlaceBidCommand, AuctionCommandResult>(new(data.Users[1], session.Id, Guid.NewGuid(), first.AuctionId!.Value, 2))).Accepted);
        await Expire(first.AuctionId.Value);
        await Close(first.AuctionId.Value);
        Assert.Equal(data.Teams[0], (await State(data, session.Id)).CurrentTeamId);
        var jump = await Send<ControlAuctionSessionCommand, AuctionCommandResult>(new(data.Users[0], session.Id, Guid.NewGuid(), "GoToTurn", TargetTeamId: data.Teams[1]));
        Assert.Equal("auction.role_full", jump.ErrorCode);
        Assert.True((await Send<ControlAuctionSessionCommand, AuctionCommandResult>(new(data.Users[0], session.Id, Guid.NewGuid(), "SkipTurn"))).Accepted);
        Assert.Equal(data.Teams[0], (await State(data, session.Id)).CurrentTeamId);
        var lastKeeper = await Start(data, session.Id, player: 1);
        Assert.True(lastKeeper.Accepted);
        await Expire(lastKeeper.AuctionId!.Value);
        await Close(lastKeeper.AuctionId.Value);
        Assert.Equal(data.Teams[1], (await State(data, session.Id)).CurrentTeamId);
        Assert.True((await Start(data, session.Id, player: 2, user: 1)).Accepted);
    }

    [Fact]
    public async Task NewSessionUsesExistingRoleCountsAndSkipsFullRoleAtOpening()
    {
        var data = await Seed();
        var session = await Create(data);
        var first = await Start(data, session.Id);
        await Expire(first.AuctionId!.Value);
        await Close(first.AuctionId.Value);
        await Send<ControlAuctionSessionCommand, AuctionCommandResult>(new(data.Users[0], session.Id, Guid.NewGuid(), "Complete"));
        var next = await Create(data);
        Assert.Equal(data.Teams[1], next.CurrentTeamId);
        Assert.Equal("P", next.CurrentRole);
        Assert.True((await Start(data, next.Id, player: 1, user: 1)).Accepted);
    }

    [Fact]
    public async Task AllFourRolesAdvanceOnlyAfterCompletionAndThenEndTheSession()
    {
        var data = await Seed(teamCount: 1, midfielders: 1, forwards: 1);
        var session = await Create(data);
        var roles = new[] { "P", "D", "C", "A" };
        for (var index = 0; index < roles.Length; index++)
        {
            Assert.Equal(roles[index], (await State(data, session.Id)).CurrentRole);
            if (index > 0)
                Assert.Equal("auction.wrong_role", (await Start(data, session.Id, player: 1)).ErrorCode);
            var call = await Start(data, session.Id, player: index * 2);
            Assert.True(call.Accepted);
            await Expire(call.AuctionId!.Value);
            await Close(call.AuctionId.Value);
        }
        var completed = await State(data, session.Id);
        Assert.Equal("Completed", completed.Status);
        Assert.Null(completed.CurrentRole);
        Assert.Null(completed.CurrentTeamId);
    }

    [Fact]
    public async Task ZeroSlotRolesAreSkippedAndExistingSessionCallerIsNormalized()
    {
        var noKeepers = await Seed(goalkeepers: 0);
        var defenderSession = await Create(noKeepers);
        Assert.Equal("D", defenderSession.CurrentRole);
        Assert.Equal("auction.wrong_role", (await Start(noKeepers, defenderSession.Id)).ErrorCode);
        Assert.True((await Start(noKeepers, defenderSession.Id, player: 2)).Accepted);

        var data = await Seed();
        var session = await Create(data);
        var call = await Start(data, session.Id);
        await Expire(call.AuctionId!.Value);
        await Close(call.AuctionId.Value);
        // Simula una sessione salvata con la vecchia rotazione, sul ruolo già completo.
        await Execute("UPDATE AuctionSessions SET CurrentPosition = 0 WHERE Id = @Id", new { session.Id });
        Assert.Equal(data.Teams[1], (await State(data, session.Id)).CurrentTeamId);
        Assert.True((await Start(data, session.Id, player: 1, user: 1)).Accepted);
    }
}
