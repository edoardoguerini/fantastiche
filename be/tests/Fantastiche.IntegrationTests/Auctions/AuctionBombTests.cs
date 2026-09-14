using Dapper;
using Fantastiche.Core.Exceptions;
using Fantastiche.Infrastructure.Auctions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Fantastiche.IntegrationTests.Auctions;

public sealed partial class AuctionEngineTests
{
    [Theory]
    [InlineData("NoSale")]
    [InlineData("Completed")]
    public async Task BombCanOnlyBeStartedOncePerTeamEvenAfterItCloses(string status)
    {
        var data = await Seed();
        var session = await Create(data);
        var command = new StartBombCommand(data.Users[0], session.Id, Guid.NewGuid(), data.Players[0]);
        var first = await Send<StartBombCommand, AuctionCommandResult>(command);
        Assert.True(first.Accepted);
        var id = first.AuctionId!.Value;
        await OpenBombCollection(id);
        if (status == "Completed") Assert.True((await BombBid(data, session.Id, id, 1, 2, user: 1)).Accepted);
        await ExpireBomb(id);
        await FinishBombReveal(data, session.Id, id);
        Assert.Equal(status, (await State(data, session.Id)).CurrentBomb!.Status);
        Assert.True((await Send<ControlAuctionSessionCommand, AuctionCommandResult>(new(data.Users[0], session.Id, Guid.NewGuid(), "GoToTurn", TargetTeamId: data.Teams[0]))).Accepted);
        var before = await State(data, session.Id);
        Assert.Equal(data.Teams.Take(1), before.UsedBombTeamIds);
        var attempts = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => StartBomb(data, session.Id, player: 1, collect: false)));
        Assert.All(attempts, result =>
        {
            Assert.False(result.Accepted);
            Assert.Equal("auction.bomb_already_used", result.ErrorCode);
        });
        Assert.Equal(before.Version, (await State(data, session.Id)).Version);
        Assert.Equal(first, await Send<StartBombCommand, AuctionCommandResult>(command));
        Assert.True((await Start(data, session.Id, player: 1)).Accepted);
        var afterClassic = await State(data, session.Id);
        Assert.Null(afterClassic.CurrentBomb);
        Assert.Equal(data.Teams.Take(1), afterClassic.UsedBombTeamIds);
    }

    [Fact]
    public async Task BombAllowanceIsIndependentForEachTeamAndSession()
    {
        var data = await Seed();
        var session = await Create(data);
        Assert.Empty(session.UsedBombTeamIds!);
        var starts = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => StartBomb(data, session.Id, collect: false)));
        var first = Assert.Single(starts, result => result.Accepted);
        await OpenBombCollection(first.AuctionId!.Value);
        await ExpireBomb(first.AuctionId.Value);
        await FinishBombReveal(data, session.Id, first.AuctionId.Value);
        Assert.Equal(data.Teams.Take(1), (await State(data, session.Id)).UsedBombTeamIds);
        var second = await StartBomb(data, session.Id, user: 1);
        Assert.True(second.Accepted);
        Assert.True((await BombBid(data, session.Id, second.AuctionId!.Value, 1, 2)).Accepted);
        Assert.True((await Send<CancelBombCommand, AuctionCommandResult>(new(data.Users[0], session.Id, Guid.NewGuid(), second.AuctionId.Value))).Accepted);
        Assert.True((await Send<ControlAuctionSessionCommand, AuctionCommandResult>(new(data.Users[0], session.Id, Guid.NewGuid(), "Complete"))).Accepted);
        var nextSession = await Create(data);
        Assert.Empty(nextSession.UsedBombTeamIds!);
        Assert.True((await StartBomb(data, nextSession.Id, collect: false)).Accepted);
    }

    [Theory]
    [InlineData("Waiting")]
    [InlineData("Collecting")]
    [InlineData("Revealing")]
    public async Task CancelledBombRestoresAllowanceAndAllowsExactlyOneConcurrentRestart(string phase)
    {
        var data = await Seed();
        var session = await Create(data);
        var command = new StartBombCommand(data.Users[0], session.Id, Guid.NewGuid(), data.Players[0]);
        var first = await Send<StartBombCommand, AuctionCommandResult>(command);
        Assert.True(first.Accepted);
        var id = first.AuctionId!.Value;
        if (phase != "Waiting") await OpenBombCollection(id);
        if (phase == "Revealing")
        {
            Assert.True((await BombBid(data, session.Id, id, 1, 2)).Accepted);
            Assert.True((await BombBid(data, session.Id, id, 1, 3, user: 1)).Accepted);
        }
        var active = await State(data, session.Id);
        Assert.Equal(phase, active.CurrentBomb!.Status);
        Assert.Equal(data.Teams.Take(1), active.UsedBombTeamIds);
        Assert.True((await Send<CancelBombCommand, AuctionCommandResult>(new(data.Users[0], session.Id, Guid.NewGuid(), id))).Accepted);
        var cancelled = await State(data, session.Id);
        Assert.Equal("Cancelled", cancelled.CurrentBomb!.Status);
        Assert.Empty(cancelled.UsedBombTeamIds!);
        Assert.Equal(first, await Send<StartBombCommand, AuctionCommandResult>(command));
        Assert.Empty((await State(data, session.Id)).UsedBombTeamIds!);
        var attempts = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => StartBomb(data, session.Id, collect: false)));
        Assert.Single(attempts, result => result.Accepted);
        Assert.Equal(data.Teams.Take(1), (await State(data, session.Id)).UsedBombTeamIds);
    }

    [Fact]
    public async Task BombOffersStaySecretIncludingFromOrganizerUntilTheirIndividualReveal()
    {
        var data = await Seed(teamCount: 3);
        var session = await Create(data);
        var start = await StartBomb(data, session.Id, collect: false);
        var id = start.AuctionId!.Value;
        Assert.Equal(start.ServerTime.AddSeconds(60), (await State(data, session.Id)).CurrentBomb!.Deadline);
        await OpenBombCollection(id);
        Assert.True((await BombBid(data, session.Id, id, 1, 7, user: 1)).Accepted);
        var organizer = (await State(data, session.Id)).CurrentBomb!;
        Assert.Null(organizer.OwnAmount);
        Assert.Null(organizer.WinningAmount);
        Assert.Null(organizer.WinningTeamId);
        Assert.Empty(organizer.RevealedOffers);
        Assert.True(organizer.Participants.Single(x => x.TeamId == data.Teams[1]).HasSubmitted);
        var personal = await Send<GetAuctionStateQuery, AuctionSessionView>(new(data.Users[1], session.Id));
        Assert.Equal(7, personal.CurrentBomb!.OwnAmount);
        var admin = await Send<GetAuctionStateQuery, AuctionSessionView>(new(fixture.Admin, session.Id));
        Assert.Null(admin.CurrentBomb!.OwnAmount);
        Assert.Empty(admin.CurrentBomb.RevealedOffers);
        await using var sql = new SqlConnection(fixture.ConnectionString);
        Assert.Equal(0, await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Bids WHERE PlayerAuctionId = @Id", new { Id = id }));
        Assert.Null(personal.CurrentAuction);

        Assert.True((await BombBid(data, session.Id, id, 1, 2)).Accepted);
        Assert.True((await BombBid(data, session.Id, id, 1, 5, user: 2)).Accepted);
        await ProgressBombs();
        var reveal = (await State(data, session.Id)).CurrentBomb!;
        Assert.Equal("Revealing", reveal.Status);
        Assert.Equal(reveal.RevealStartedAt!.Value.AddSeconds(30), reveal.NextRevealAt);
        Assert.Empty(reveal.RevealedOffers);
        Assert.Equal(data.Teams, reveal.Participants.Select(x => x.TeamId));
        var beforeReveal = await State(data, session.Id);
        await RevealBombStep(id);
        var afterReveal = await State(data, session.Id);
        var first = afterReveal.CurrentBomb!;
        Assert.InRange(first.NextRevealAt!.Value,
            beforeReveal.ServerTime.AddSeconds(6), afterReveal.ServerTime.AddSeconds(6));
        Assert.Equal(2, Assert.Single(first.RevealedOffers).Amount);
        Assert.Null(first.WinningTeamId);
        await RevealBombStep(id);
        var second = (await State(data, session.Id)).CurrentBomb!;
        Assert.Equal(new[] { 2, 5 }, second.RevealedOffers.Select(x => x.Amount));
        Assert.Null(second.WinningAmount);
        await RevealBombStep(id);
        var third = (await State(data, session.Id)).CurrentBomb!;
        Assert.Equal(new[] { 2, 5, 7 }, third.RevealedOffers.Select(x => x.Amount));
        Assert.Equal("Revealing", third.Status);
        Assert.All((await State(data, session.Id)).Teams, x => Assert.Equal(10, x.Budget));
        await RevealBombStep(id);
        var final = await State(data, session.Id);
        Assert.Equal("Completed", final.CurrentBomb!.Status);
        Assert.Equal(data.Teams[1], final.CurrentBomb.WinningTeamId);
        Assert.Equal(7, final.CurrentBomb.WinningAmount);
        Assert.Equal("Closed", final.CurrentAuction!.Status);
        Assert.Equal(3, final.Teams.Single(x => x.Id == data.Teams[1]).Budget);
    }

    [Fact]
    public async Task BombConfirmationIsImmutableAndConcurrentRetriesDoNotDuplicateIt()
    {
        var data = await Seed();
        var session = await Create(data);
        var id = (await StartBomb(data, session.Id)).AuctionId!.Value;
        var command = new SubmitBombOfferCommand(data.Users[0], session.Id, Guid.NewGuid(), id, 1, 5);
        var duplicates = await Task.WhenAll(Enumerable.Range(0, 6)
            .Select(_ => Send<SubmitBombOfferCommand, AuctionCommandResult>(command)));
        Assert.All(duplicates, result => Assert.Equal(duplicates[0], result));
        Assert.True(duplicates[0].Accepted);
        var changed = await Send<SubmitBombOfferCommand, AuctionCommandResult>(command with { Amount = 6 });
        Assert.Equal("auction.request_conflict", changed.ErrorCode);
        Assert.False((await BombBid(data, session.Id, id, 1, 6)).Accepted);
        Assert.Equal(5, (await State(data, session.Id)).CurrentBomb!.OwnAmount);
        Assert.Equal(duplicates[0], await Send<GetAuctionReceiptQuery, AuctionCommandResult>(new(data.Users[0], session.Id, command.RequestId)));
    }

    [Fact]
    public async Task BombHighestTieStartsSecretRunoffOnlyForTiedTeamsAndRejectsOldRound()
    {
        var data = await Seed(teamCount: 3);
        var session = await Create(data);
        var id = (await StartBomb(data, session.Id)).AuctionId!.Value;
        Assert.True((await BombBid(data, session.Id, id, 1, 5)).Accepted);
        Assert.True((await BombBid(data, session.Id, id, 1, 5, user: 1)).Accepted);
        Assert.True((await BombBid(data, session.Id, id, 1, 2, user: 2)).Accepted);
        await FinishBombReveal(data, session.Id, id);
        var runoff = (await State(data, session.Id)).CurrentBomb!;
        Assert.Equal("Collecting", runoff.Status);
        Assert.Equal(2, runoff.Round);
        Assert.Equal(5, runoff.MinimumAmount);
        Assert.Equal(data.Teams.Take(2), runoff.Participants.Select(x => x.TeamId));
        Assert.All(runoff.Participants, x => Assert.False(x.HasSubmitted));
        Assert.Null(runoff.OwnAmount);
        Assert.Empty(runoff.RevealedOffers);
        Assert.InRange((runoff.Deadline - (await State(data, session.Id)).ServerTime).TotalSeconds, 55, 60);
        Assert.False((await BombBid(data, session.Id, id, 1, 8)).Accepted);
        Assert.False((await BombBid(data, session.Id, id, 2, 4)).Accepted);
        Assert.False((await BombBid(data, session.Id, id, 2, 8, user: 2)).Accepted);
        Assert.True((await BombBid(data, session.Id, id, 2, 6)).Accepted);
        Assert.True((await BombBid(data, session.Id, id, 2, 8, user: 1)).Accepted);
        await FinishBombReveal(data, session.Id, id);
        var completed = await State(data, session.Id);
        Assert.Equal("Completed", completed.CurrentBomb!.Status);
        Assert.Equal(8, completed.CurrentBomb.WinningAmount);
        Assert.Equal(10, completed.Teams.Single(x => x.Id == data.Teams[0]).Budget);
        Assert.Equal(2, completed.Teams.Single(x => x.Id == data.Teams[1]).Budget);
        Assert.Equal(data.Teams[2], completed.CurrentTeamId);
    }

    [Fact]
    public async Task BombRepeatedTieDoesNotAssignArbitrarily()
    {
        var data = await Seed();
        var session = await Create(data);
        var id = (await StartBomb(data, session.Id)).AuctionId!.Value;
        for (var round = 1; round <= 2; round++)
        {
            Assert.True((await BombBid(data, session.Id, id, round, 9)).Accepted);
            Assert.True((await BombBid(data, session.Id, id, round, 9, user: 1)).Accepted);
            await FinishBombReveal(data, session.Id, id);
        }
        var state = await State(data, session.Id);
        Assert.Equal(3, state.CurrentBomb!.Round);
        Assert.Equal("Collecting", state.CurrentBomb.Status);
        Assert.Null(state.CurrentBomb.WinningTeamId);
        Assert.Null(state.CurrentAuction);
        Assert.All(state.Teams, team => Assert.Equal(10, team.Budget));
    }

    [Fact]
    public async Task BombDeadlineExcludesMissingOffersAndRejectsLateConfirmation()
    {
        var data = await Seed();
        var session = await Create(data);
        var id = (await StartBomb(data, session.Id)).AuctionId!.Value;
        Assert.True((await BombBid(data, session.Id, id, 1, 4)).Accepted);
        await ExpireBomb(id);
        Assert.False((await BombBid(data, session.Id, id, 1, 9, user: 1)).Accepted);
        await FinishBombReveal(data, session.Id, id);
        var state = await State(data, session.Id);
        Assert.Equal("Completed", state.CurrentBomb!.Status);
        Assert.Equal(data.Teams[0], state.CurrentBomb.WinningTeamId);
        Assert.Equal(4, state.CurrentBomb.WinningAmount);
        Assert.False(state.CurrentBomb.Participants.Single(x => x.TeamId == data.Teams[1]).HasSubmitted);
    }

    [Fact]
    public async Task EmptyBombExpiresWithoutChargeAndPlayerRemainsAvailable()
    {
        var data = await Seed();
        var session = await Create(data);
        var id = (await StartBomb(data, session.Id)).AuctionId!.Value;
        await ExpireBomb(id);
        await FinishBombReveal(data, session.Id, id);
        var state = await State(data, session.Id);
        Assert.Equal("NoSale", state.CurrentBomb!.Status);
        Assert.Null(state.CurrentAuction);
        Assert.All(state.Teams, x => Assert.Equal(10, x.Budget));
        Assert.True((await Start(data, session.Id, user: 1)).Accepted);
        Assert.Null((await State(data, session.Id)).CurrentBomb);
    }

    [Fact]
    public async Task BombCancellationRequiresOrganizerAndNeverDebitsOrRevealsSealedAmounts()
    {
        var data = await Seed();
        var session = await Create(data);
        var id = (await StartBomb(data, session.Id)).AuctionId!.Value;
        Assert.True((await BombBid(data, session.Id, id, 1, 7, user: 1)).Accepted);
        Assert.False((await Send<CancelBombCommand, AuctionCommandResult>(new(data.Users[1], session.Id, Guid.NewGuid(), id))).Accepted);
        var cancel = new CancelBombCommand(data.Users[0], session.Id, Guid.NewGuid(), id);
        var result = await Send<CancelBombCommand, AuctionCommandResult>(cancel);
        Assert.True(result.Accepted);
        Assert.Equal(result, await Send<CancelBombCommand, AuctionCommandResult>(cancel));
        var state = await State(data, session.Id);
        Assert.Equal("Cancelled", state.CurrentBomb!.Status);
        Assert.Empty(state.CurrentBomb.RevealedOffers);
        Assert.Null(state.CurrentBomb.WinningAmount);
        Assert.All(state.Teams, x => Assert.Equal(10, x.Budget));
        Assert.Equal(data.Teams[0], state.CurrentTeamId);
        Assert.True((await Start(data, session.Id)).Accepted);
        Assert.Null((await State(data, session.Id)).CurrentBomb);
    }

    [Fact]
    public async Task ActiveBombReservesCatalogPlayerAndCancelledRevealKeepsUnrevealedAmountsPrivate()
    {
        var data = await Seed();
        var session = await Create(data);
        var id = (await StartBomb(data, session.Id)).AuctionId!.Value;
        var available = await Send<GetAuctionCatalogQuery, AuctionPage<AuctionCatalogPlayerView>>(new(data.Users[0], session.Id));
        Assert.DoesNotContain(available.Items, x => x.PlayerId == data.Players[0]);
        var all = await Send<GetAuctionCatalogQuery, AuctionPage<AuctionCatalogPlayerView>>(new(data.Users[0], session.Id, AvailableOnly: false));
        var reserved = Assert.Single(all.Items, x => x.PlayerId == data.Players[0]);
        Assert.False(reserved.IsAvailable);
        Assert.Null(reserved.TeamId);
        Assert.True((await BombBid(data, session.Id, id, 1, 2)).Accepted);
        Assert.True((await BombBid(data, session.Id, id, 1, 8, user: 1)).Accepted);
        await ProgressBombs();
        await RevealBombStep(id);
        Assert.True((await Send<CancelBombCommand, AuctionCommandResult>(new(data.Users[0], session.Id, Guid.NewGuid(), id))).Accepted);
        var cancelled = await State(data, session.Id);
        Assert.Equal("Cancelled", cancelled.CurrentBomb!.Status);
        Assert.Equal(2, Assert.Single(cancelled.CurrentBomb.RevealedOffers).Amount);
        Assert.Null(cancelled.CurrentBomb.WinningAmount);
        Assert.All(cancelled.Teams, x => Assert.Equal(10, x.Budget));
        available = await Send<GetAuctionCatalogQuery, AuctionPage<AuctionCatalogPlayerView>>(new(data.Users[0], session.Id));
        Assert.Contains(available.Items, x => x.PlayerId == data.Players[0]);
    }

    [Fact]
    public async Task BombAndClassicAuctionCannotOverlapEvenWithConcurrentStarts()
    {
        var data = await Seed();
        var session = await Create(data);
        var starts = await Task.WhenAll(StartBomb(data, session.Id), Start(data, session.Id));
        Assert.Single(starts, x => x.Accepted);
        var state = await State(data, session.Id);
        if (state.CurrentBomb is not null)
        {
            Assert.Null(state.CurrentAuction);
            foreach (var action in new[] { "Pause", "SkipTurn", "Complete", "GoToTurn" })
                Assert.False((await Send<ControlAuctionSessionCommand, AuctionCommandResult>(new(data.Users[0], session.Id, Guid.NewGuid(), action, TargetTeamId: data.Teams[1]))).Accepted);
            Assert.False((await StartBomb(data, session.Id)).Accepted);
        }
        else Assert.Equal("Open", state.CurrentAuction!.Status);
    }

    [Fact]
    public async Task BombValidatesTurnRoleBudgetAndIsolationBeforeWriting()
    {
        var data = await Seed();
        var other = await Seed();
        var session = await Create(data);
        var otherSession = await Create(other);
        Assert.False((await StartBomb(data, session.Id, user: 1)).Accepted);
        Assert.False((await StartBomb(data, session.Id, player: 2)).Accepted);
        Assert.Null((await State(data, session.Id)).CurrentBomb);
        var id = (await StartBomb(data, session.Id)).AuctionId!.Value;
        Assert.False((await BombBid(data, session.Id, id, 1, 10)).Accepted);
        Assert.False((await BombBid(data, session.Id, id, 1, 0)).Accepted);
        Assert.Null((await State(data, session.Id)).CurrentBomb!.OwnAmount);
        await Assert.ThrowsAsync<DomainException>(() => Send<GetAuctionStateQuery, AuctionSessionView>(new(other.Users[0], session.Id)));
        await Assert.ThrowsAsync<DomainException>(() => Send<SubmitBombOfferCommand, AuctionCommandResult>(new(other.Users[0], session.Id, Guid.NewGuid(), id, 1, 2)));
        Assert.False((await Send<SubmitBombOfferCommand, AuctionCommandResult>(new(other.Users[0], otherSession.Id, Guid.NewGuid(), id, 1, 2))).Accepted);
    }

    [Fact]
    public async Task BombConcurrentFinalizationAwardsOnlyOnceAndSkipsWinnerFullRole()
    {
        var data = await Seed();
        var session = await Create(data);
        Assert.True((await Send<ControlAuctionSessionCommand, AuctionCommandResult>(new(data.Users[0], session.Id, Guid.NewGuid(), "GoToTurn", TargetTeamId: data.Teams[1]))).Accepted);
        var id = (await StartBomb(data, session.Id, user: 1)).AuctionId!.Value;
        var confirmations = await Task.WhenAll(BombBid(data, session.Id, id, 1, 2), BombBid(data, session.Id, id, 1, 7, user: 1));
        Assert.All(confirmations, x => Assert.True(x.Accepted));
        await ProgressBombs();
        await RevealBombStep(id);
        await RevealBombStep(id);
        await MakeBombRevealDue(id);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => ProgressBombs()));
        var state = await State(data, session.Id);
        Assert.Equal("Completed", state.CurrentBomb!.Status);
        Assert.Equal(data.Teams[0], state.CurrentTeamId);
        Assert.Equal(state.CurrentBomb.PlayerAuctionId, state.CurrentAuction!.Id);
        await using var sql = new SqlConnection(fixture.ConnectionString);
        var args = new { Id = state.CurrentAuction.Id };
        Assert.Equal(1, await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM RosterEntries WHERE PlayerAuctionId = @Id", args));
        Assert.Equal(1, await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM BudgetMovements WHERE PlayerAuctionId = @Id", args));
        Assert.Equal(-7, await sql.ExecuteScalarAsync<int>("SELECT SUM(Amount) FROM BudgetMovements WHERE PlayerAuctionId = @Id", args));
        Assert.True((await StartBomb(data, session.Id, player: 1)).Accepted);
        var next = (await State(data, session.Id)).CurrentBomb!;
        Assert.Equal(data.Teams[0], Assert.Single(next.Participants).TeamId);
        Assert.False((await BombBid(data, session.Id, next.Id, 1, 1, user: 1)).Accepted);
    }

    [Fact]
    public async Task BombWaitingLobbyLastsSixtySecondsAndReplayCannotRestartItsDeadline()
    {
        var data = await Seed();
        var session = await Create(data);
        var command = new StartBombCommand(data.Users[0], session.Id, Guid.NewGuid(), data.Players[0]);
        var start = await Send<StartBombCommand, AuctionCommandResult>(command);
        Assert.True(start.Accepted);
        var waiting = await State(data, session.Id);
        var bomb = waiting.CurrentBomb!;
        Assert.Equal("Waiting", bomb.Status);
        Assert.Equal(start.ServerTime.AddSeconds(60), bomb.Deadline);
        Assert.Equal(1, bomb.Round);
        Assert.Equal(data.Teams, bomb.Participants.Select(x => x.TeamId));
        Assert.All(bomb.Participants, x => Assert.False(x.HasSubmitted));
        Assert.Null(bomb.OwnAmount);
        Assert.Null(bomb.RevealStartedAt);
        Assert.Empty(bomb.RevealedOffers);
        Assert.Null(waiting.CurrentAuction);
        Assert.All(waiting.Teams, x => Assert.Equal(10, x.Budget));
        var early = await BombBid(data, session.Id, bomb.Id, 1, 2);
        Assert.False(early.Accepted);
        Assert.Equal("auction.bomb_not_collecting", early.ErrorCode);
        Assert.False((await Start(data, session.Id)).Accepted);
        Assert.False((await Send<ControlAuctionSessionCommand, AuctionCommandResult>(new(data.Users[0], session.Id, Guid.NewGuid(), "SkipTurn"))).Accepted);
        await ProgressBombs();
        Assert.Equal(start, await Send<StartBombCommand, AuctionCommandResult>(command));
        var reconnect = await Send<GetAuctionStateQuery, AuctionSessionView>(new(data.Users[1], session.Id));
        Assert.Equal("Waiting", reconnect.CurrentBomb!.Status);
        Assert.Equal(bomb.Deadline, reconnect.CurrentBomb.Deadline);
        Assert.Equal(waiting.Version, reconnect.Version);
        var catalog = await Send<GetAuctionCatalogQuery, AuctionPage<AuctionCatalogPlayerView>>(new(data.Users[0], session.Id));
        Assert.DoesNotContain(catalog.Items, x => x.PlayerId == data.Players[0]);
    }

    [Fact]
    public async Task BombConcurrentWaitingExpiryStartsOneFullCollectionMinute()
    {
        var data = await Seed();
        var session = await Create(data);
        var id = (await StartBomb(data, session.Id, collect: false)).AuctionId!.Value;
        var waiting = await State(data, session.Id);
        await ExpireBomb(id);
        var transitions = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => ProgressBombs()));
        Assert.Equal(1, transitions.Sum());
        var collecting = await State(data, session.Id);
        Assert.Equal("Collecting", collecting.CurrentBomb!.Status);
        Assert.Equal(waiting.Version + 1, collecting.Version);
        Assert.Equal(1, collecting.CurrentBomb.Round);
        Assert.InRange((collecting.CurrentBomb.Deadline - collecting.ServerTime).TotalSeconds, 55, 60);
        await ProgressBombs();
        Assert.Equal(collecting.CurrentBomb.Deadline, (await State(data, session.Id)).CurrentBomb!.Deadline);
        Assert.True((await BombBid(data, session.Id, id, 1, 2)).Accepted);
    }

    [Fact]
    public async Task BombCancelledDuringWaitingNeverStartsAndKeepsCallerAndBudget()
    {
        var data = await Seed();
        var session = await Create(data);
        var id = (await StartBomb(data, session.Id, collect: false)).AuctionId!.Value;
        Assert.True((await Send<CancelBombCommand, AuctionCommandResult>(new(data.Users[0], session.Id, Guid.NewGuid(), id))).Accepted);
        await ExpireBomb(id);
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => ProgressBombs()));
        var cancelled = await State(data, session.Id);
        Assert.Equal("Cancelled", cancelled.CurrentBomb!.Status);
        Assert.Equal(data.Teams[0], cancelled.CurrentTeamId);
        Assert.Null(cancelled.CurrentAuction);
        Assert.All(cancelled.Teams, x => Assert.Equal(10, x.Budget));
        Assert.False((await BombBid(data, session.Id, id, 1, 2)).Accepted);
        Assert.True((await Start(data, session.Id)).Accepted);
    }

    private async Task<AuctionCommandResult> StartBomb(Scenario data, Guid sessionId, int player = 0, int user = 0, bool collect = true)
    {
        var result = await Send<StartBombCommand, AuctionCommandResult>(new(data.Users[user], sessionId, Guid.NewGuid(), data.Players[player]));
        if (result.Accepted && collect) await OpenBombCollection(result.AuctionId!.Value);
        return result;
    }

    private async Task OpenBombCollection(Guid id)
    {
        await ExpireBomb(id);
        await ProgressBombs();
    }

    private Task<AuctionCommandResult> BombBid(Scenario data, Guid sessionId, Guid bombId, int round, int amount, int user = 0) =>
        Send<SubmitBombOfferCommand, AuctionCommandResult>(new(data.Users[user], sessionId, Guid.NewGuid(), bombId, round, amount));

    private async Task<int> ProgressBombs()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AuctionEngine>().AdvanceBombsAsync(default);
    }

    private Task ExpireBomb(Guid id) => Execute("UPDATE BombAuctions SET Deadline = DATEADD(second, -1, TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')) WHERE Id = @id", new { id });

    private Task MakeBombRevealDue(Guid id) => Execute("UPDATE BombAuctions SET NextRevealAt = DATEADD(second, -1, TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')) WHERE Id = @id AND Status = 1", new { id });

    private async Task RevealBombStep(Guid id)
    {
        await MakeBombRevealDue(id);
        await ProgressBombs();
    }

    private async Task FinishBombReveal(Scenario data, Guid sessionId, Guid id)
    {
        await ProgressBombs();
        for (var step = 0; step < data.Teams.Length + 2; step++)
        {
            if ((await State(data, sessionId)).CurrentBomb!.Status != "Revealing") return;
            await RevealBombStep(id);
        }
        Assert.NotEqual("Revealing", (await State(data, sessionId)).CurrentBomb!.Status);
    }
}
