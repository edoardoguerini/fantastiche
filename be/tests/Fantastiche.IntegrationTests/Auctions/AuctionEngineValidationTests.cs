using Fantastiche.Core.Exceptions;
using Fantastiche.Infrastructure.Auctions;

namespace Fantastiche.IntegrationTests.Auctions;

public sealed partial class AuctionEngineTests
{
    [Fact]
    public async Task CreateValidatesFrozenParticipantsAndAllowsOnlyOneActiveSession()
    {
        var data = await Seed();
        Assert.Equal(403, (await Assert.ThrowsAsync<DomainException>(() => Send<CreateAuctionSessionCommand, AuctionSessionView>(
            new(data.Users[1], data.LeagueId, data.SeasonId, data.Teams)))).StatusCode);
        Assert.Equal(400, (await Assert.ThrowsAsync<DomainException>(() => Send<CreateAuctionSessionCommand, AuctionSessionView>(
            new(data.Users[0], data.LeagueId, data.SeasonId, [data.Teams[0], data.Teams[0]])))).StatusCode);
        var other = await Seed();
        Assert.Equal(409, (await Assert.ThrowsAsync<DomainException>(() => Send<CreateAuctionSessionCommand, AuctionSessionView>(
            new(data.Users[0], data.LeagueId, data.SeasonId, [other.Teams[0]])))).StatusCode);
        var session = await Create(data);
        Assert.Equal(409, (await Assert.ThrowsAsync<DomainException>(() => Create(data))).StatusCode);
        var same = await Send<GetActiveAuctionQuery, AuctionSessionView?>(new(data.Users[0], data.LeagueId, data.SeasonId));
        Assert.Equal(session.Id, same!.Id);
        Assert.Equal(data.Teams, same.TeamOrder);
    }

    [Fact]
    public async Task InvalidCommandsAreReceiptedAndReusingRequestIdForAnotherCommandConflicts()
    {
        var data = await Seed();
        var session = await Create(data);
        var request = new StartPlayerAuctionCommand(data.Users[0], session.Id, Guid.NewGuid(), data.Players[0], 7, [1, 1]);
        var result = await Send<StartPlayerAuctionCommand, AuctionCommandResult>(request);
        Assert.False(result.Accepted);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(result, await Send<StartPlayerAuctionCommand, AuctionCommandResult>(request with { Context = data.Users[0] with { CorrelationId = "changed" } }));
        Assert.Equal("auction.request_conflict", (await Send<ControlAuctionSessionCommand, AuctionCommandResult>(new(data.Users[0], session.Id, request.RequestId, "Pause"))).ErrorCode);
        Assert.Equal(1, (await State(data, session.Id)).Version);
        Assert.Equal(400, (await Assert.ThrowsAsync<DomainException>(() => Send<StartPlayerAuctionCommand, AuctionCommandResult>(request with { RequestId = Guid.Empty }))).StatusCode);
        var nonOrganizer = await Send<ControlAuctionSessionCommand, AuctionCommandResult>(new(data.Users[1], session.Id, Guid.NewGuid(), "Pause"));
        Assert.Equal(403, nonOrganizer.StatusCode);
        Assert.Equal(nonOrganizer, await Send<GetAuctionReceiptQuery, AuctionCommandResult>(new(data.Users[1], session.Id, nonOrganizer.RequestId)));
    }

    [Fact]
    public async Task SkipWrapsAndReorderMustPreserveExactlyTheParticipantSet()
    {
        var data = await Seed(teamCount: 3);
        var session = await Create(data);
        for (var i = 1; i <= 3; i++)
        {
            Assert.True((await Send<ControlAuctionSessionCommand, AuctionCommandResult>(new(data.Users[0], session.Id, Guid.NewGuid(), "SkipTurn"))).Accepted);
            Assert.Equal(data.Teams[i % 3], (await State(data, session.Id)).CurrentTeamId);
        }
        var before = await State(data, session.Id);
        var invalid = await Send<ControlAuctionSessionCommand, AuctionCommandResult>(new(data.Users[0], session.Id, Guid.NewGuid(), "Reorder", [data.Teams[0], data.Teams[0], data.Teams[1]]));
        Assert.False(invalid.Accepted);
        Assert.Equal(before.Version, invalid.Version);
        var valid = await Send<ControlAuctionSessionCommand, AuctionCommandResult>(new(data.Users[0], session.Id, Guid.NewGuid(), "Reorder", data.Teams.Reverse().ToArray()));
        Assert.True(valid.Accepted);
        var after = await State(data, session.Id);
        Assert.Equal(before.CurrentTeamId, after.CurrentTeamId);
        Assert.Equal(data.Teams.Reverse(), after.TeamOrder);
    }
    [Fact]
    public async Task MembershipDoesNotAddParticipantsAndForeignAuctionIdsCannotChangeThisSession()
    {
        var data = await Seed(teamCount: 3);
        var session = await Send<CreateAuctionSessionCommand, AuctionSessionView>(new(data.Users[0], data.LeagueId, data.SeasonId, data.Teams.Take(2).ToArray()));
        var first = await Start(data, session.Id);
        var unlisted = await Send<PlaceBidCommand, AuctionCommandResult>(new(data.Users[2], session.Id, Guid.NewGuid(), first.AuctionId!.Value, 2));
        Assert.Equal("auction.not_participant", unlisted.ErrorCode);
        Assert.Equal(403, unlisted.StatusCode);
        var foreign = await Seed();
        var otherSession = await Create(foreign);
        var otherAuction = await Start(foreign, otherSession.Id);
        var wrongScope = await Send<PlaceBidCommand, AuctionCommandResult>(new(data.Users[0], session.Id, Guid.NewGuid(), otherAuction.AuctionId!.Value, 2));
        Assert.False(wrongScope.Accepted);
        Assert.Equal(404, wrongScope.StatusCode);
        Assert.Equal(first.Version, (await State(data, session.Id)).Version);
        Assert.Equal(otherAuction.Version, (await State(foreign, otherSession.Id)).Version);
    }
}
