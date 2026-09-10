using Fantastiche.Core.Auth;
using Fantastiche.Core.Exceptions;
using Fantastiche.Infrastructure.Auctions;
using Microsoft.Extensions.DependencyInjection;

namespace Fantastiche.IntegrationTests.Auctions;

public sealed partial class AuctionEngineTests
{
    [Fact]
    public async Task CallerMembershipScopeAndOrganizerControlsAreEnforcedWithDurableRejections()
    {
        var data = await Seed();
        var session = await Create(data);
        Assert.Equal("auction.not_caller", (await Start(data, session.Id, user: 1)).ErrorCode);
        Assert.Equal("auction.not_participant", (await Send<StartPlayerAuctionCommand, AuctionCommandResult>(
            new(fixture.Admin, session.Id, Guid.NewGuid(), data.Players[0], 30, [1]))).ErrorCode);
        Assert.Equal(1, (await State(data, session.Id)).Version);
        var start = await Start(data, session.Id);
        Assert.Equal("auction.player_in_progress", (await Start(data, session.Id, player: 2)).ErrorCode);
        foreach (var action in new[] { "Pause", "SkipTurn", "Reorder", "Complete" })
            Assert.Equal("auction.player_in_progress", (await Send<ControlAuctionSessionCommand, AuctionCommandResult>(
                new(data.Users[0], session.Id, Guid.NewGuid(), action, data.Teams.Reverse().ToArray()))).ErrorCode);
        var outsider = await Seed();
        Assert.Equal(403, (await Assert.ThrowsAsync<DomainException>(() => Send<PlaceBidCommand, AuctionCommandResult>(
            new(outsider.Users[0], session.Id, Guid.NewGuid(), start.AuctionId!.Value, 2)))).StatusCode);
        await Execute("UPDATE LeagueMembers SET Status = 0 WHERE LeagueId = @LeagueId AND UserId = @UserId", new { data.LeagueId, data.Users[1].UserId });
        Assert.Equal(403, (await Assert.ThrowsAsync<DomainException>(() => Send<PlaceBidCommand, AuctionCommandResult>(
            new(data.Users[1], session.Id, Guid.NewGuid(), start.AuctionId!.Value, 2)))).StatusCode);
        await Expire(start.AuctionId!.Value);
        await Close(start.AuctionId.Value);
        var denied = await Send<ControlAuctionSessionCommand, AuctionCommandResult>(new(new RequestContext(data.Users[0].UserId, false, "different"), session.Id, Guid.NewGuid(), "Pause"));
        Assert.True(denied.Accepted);
        var paused = await State(data, session.Id);
        var noOp = await Send<ControlAuctionSessionCommand, AuctionCommandResult>(new(data.Users[0], session.Id, Guid.NewGuid(), "Pause"));
        Assert.Equal(paused.Version, noOp.Version);
        Assert.Equal("auction.not_active", (await Start(data, session.Id, player: 2, user: 0)).ErrorCode);
        var reorder = await Send<ControlAuctionSessionCommand, AuctionCommandResult>(new(data.Users[0], session.Id, Guid.NewGuid(), "Reorder", data.Teams.Reverse().ToArray()));
        Assert.True(reorder.Accepted);
        Assert.Equal(paused.CurrentTeamId, (await State(data, session.Id)).CurrentTeamId);
        Assert.True((await Send<ControlAuctionSessionCommand, AuctionCommandResult>(new(data.Users[0], session.Id, Guid.NewGuid(), "Resume"))).Accepted);
        Assert.True((await Send<ControlAuctionSessionCommand, AuctionCommandResult>(new(data.Users[0], session.Id, Guid.NewGuid(), "Complete"))).Accepted);
        Assert.Null((await State(data, session.Id)).CurrentTeamId);
    }

    [Fact]
    public async Task BudgetReserveRoleCapacityTurnsAndAutomaticCompletionRemainConsistent()
    {
        var data = await Seed();
        var session = await Create(data);
        var first = await Start(data, session.Id);
        var max = await Send<PlaceBidCommand, AuctionCommandResult>(new(data.Users[0], session.Id, Guid.NewGuid(), first.AuctionId!.Value, 9));
        Assert.True(max.Accepted);
        await Expire(first.AuctionId.Value);
        await Close(first.AuctionId.Value);
        var second = await Start(data, session.Id, player: 1, user: 1);
        Assert.True(second.Accepted);
        Assert.Equal("auction.role_full", (await Send<PlaceBidCommand, AuctionCommandResult>(new(data.Users[0], session.Id, Guid.NewGuid(), second.AuctionId!.Value, 2))).ErrorCode);
        await Expire(second.AuctionId.Value);
        await Close(second.AuctionId.Value);
        var third = await Start(data, session.Id, player: 2);
        Assert.True(third.Accepted);
        await Expire(third.AuctionId!.Value);
        await Close(third.AuctionId.Value);
        var fourth = await Start(data, session.Id, player: 3, user: 1);
        Assert.True(fourth.Accepted);
        await Expire(fourth.AuctionId!.Value);
        await Close(fourth.AuctionId.Value);
        var state = await State(data, session.Id);
        Assert.Equal("Completed", state.Status);
        Assert.Null(state.CurrentTeamId);
        Assert.Equal(0, state.Teams.Single(x => x.Id == data.Teams[0]).Budget);
        Assert.All(state.Teams, team => { Assert.Equal(1, team.Goalkeepers); Assert.Equal(1, team.Defenders); });
    }

    [Fact]
    public async Task ExpiredWorkerRecoversDueAuctionsAndLeavesFutureAuctionsOpen()
    {
        var data = await Seed();
        var session = await Create(data);
        var first = await Start(data, session.Id);
        await Expire(first.AuctionId!.Value);
        await using var scope = fixture.Services.CreateAsyncScope();
        var engine = scope.ServiceProvider.GetRequiredService<AuctionEngine>();
        Assert.Equal(1, await engine.CloseExpiredAsync(default));
        Assert.Equal(0, await engine.CloseExpiredAsync(default));
        var second = await Start(data, session.Id, player: 1, user: 1);
        Assert.True(second.Accepted);
        Assert.Equal(0, await engine.CloseExpiredAsync(default));
    }
}
