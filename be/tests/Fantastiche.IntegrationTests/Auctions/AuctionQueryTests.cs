using System.Text.Json;
using Fantastiche.Core.Auth;
using Fantastiche.Core.Exceptions;
using Fantastiche.Infrastructure.Auctions;
using Fantastiche.Infrastructure.Catalog;
using Fantastiche.Infrastructure.Common;
using Fantastiche.Infrastructure.Common.Authentication;
using Fantastiche.Infrastructure.Common.Persistence;
using Fantastiche.Infrastructure.Leagues;
using Fantastiche.Infrastructure.Teams;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fantastiche.IntegrationTests.Auctions;

public sealed partial class AuctionQueryTests(SqlFixture fixture) : IClassFixture<SqlFixture>
{
    private static readonly JsonSerializerOptions AqJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task StateAndActiveQueriesReturnOneCoherentSnapshotIncludingTheLatestClosedAuction()
    {
        var scenario = await AqCreateScenario();

        var state = await AqQuery<GetAuctionStateQuery, AuctionSessionView>(
            new(scenario.FirstUser, scenario.ActiveSessionId));
        var active = await AqQuery<GetActiveAuctionQuery, AuctionSessionView?>(
            new(scenario.FirstUser, scenario.LeagueId, scenario.SeasonId));

        Assert.Equal(scenario.ActiveSessionId, state.Id);
        Assert.NotNull(active);
        Assert.Equal(state.Id, active.Id);
        Assert.Equal(state.Version, active.Version);
        Assert.Equal(state.TeamOrder, active.TeamOrder);
        Assert.Equal(state.CurrentAuction!.Id, active.CurrentAuction!.Id);
        Assert.Equal("Active", state.Status);
        Assert.Equal(4, state.Version);
        Assert.Equal(scenario.SecondTeamId, state.CurrentTeamId);
        Assert.Equal([scenario.FirstTeamId, scenario.SecondTeamId], state.TeamOrder);
        Assert.Equal(scenario.CurrentAuctionId, state.CurrentAuction!.Id);
        Assert.Equal("Closed", state.CurrentAuction.Status);
        Assert.Equal("Difensore attuale", state.CurrentAuction.Name);
        Assert.Equal(new DateTime(2001, 1, 1), state.CurrentAuction.BirthDate);
        Assert.Equal("Italia", state.CurrentAuction.Nationality);
        Assert.Equal("Sinistro", state.CurrentAuction.PreferredFoot);
        Assert.Equal([1, 3], state.CurrentAuction.Increments);
        Assert.Equal(TimeSpan.Zero, state.ServerTime.Offset);

        var first = Assert.Single(state.Teams, x => x.Id == scenario.FirstTeamId);
        Assert.Equal(497, first.Budget);
        Assert.Equal(1, first.Goalkeepers);
        var second = Assert.Single(state.Teams, x => x.Id == scenario.SecondTeamId);
        Assert.Equal(495, second.Budget);
        Assert.Equal(1, second.Defenders);
    }

    [Fact]
    public async Task QueriesRequireAnActiveLeagueMemberAndActiveLookupAuthorizesBeforeReturningNull()
    {
        var scenario = await AqCreateScenario();

        var anonymous = await Assert.ThrowsAsync<DomainException>(() =>
            AqQuery<GetAuctionStateQuery, AuctionSessionView>(
                new(new RequestContext(Guid.Empty, false, "auction-query-test"), scenario.ActiveSessionId)));
        Assert.Equal(401, anonymous.StatusCode);
        var pending = await Assert.ThrowsAsync<DomainException>(() =>
            AqQuery<GetAuctionStateQuery, AuctionSessionView>(new(scenario.PendingUser, scenario.ActiveSessionId)));
        Assert.Equal(403, pending.StatusCode);
        var staleAdmin = await Assert.ThrowsAsync<DomainException>(() =>
            AqQuery<GetAuctionStateQuery, AuctionSessionView>(
                new(scenario.PendingUser with { IsSuperAdmin = true }, scenario.ActiveSessionId)));
        Assert.Equal(403, staleAdmin.StatusCode);

        var pendingWithoutSession = await Assert.ThrowsAsync<DomainException>(() =>
            AqQuery<GetActiveAuctionQuery, AuctionSessionView?>(
                new(scenario.PendingUser, scenario.LeagueId, scenario.EmptySeasonId)));
        Assert.Equal(403, pendingWithoutSession.StatusCode);

        Assert.Null(await AqQuery<GetActiveAuctionQuery, AuctionSessionView?>(
            new(scenario.FirstUser, scenario.LeagueId, scenario.EmptySeasonId)));
    }

    [Fact]
    public async Task ReceiptCanOnlyBeReadByItsUserAndUsesWebJsonContract()
    {
        var scenario = await AqCreateScenario();

        var receipt = await AqQuery<GetAuctionReceiptQuery, AuctionCommandResult>(
            new(scenario.FirstUser, scenario.ActiveSessionId, scenario.RequestId));

        Assert.Equal(scenario.StoredResult, receipt);
        var hidden = await Assert.ThrowsAsync<DomainException>(() =>
            AqQuery<GetAuctionReceiptQuery, AuctionCommandResult>(
                new(scenario.SecondUser, scenario.ActiveSessionId, scenario.RequestId)));
        Assert.Equal(404, hidden.StatusCode);
        var hiddenFromAdmin = await Assert.ThrowsAsync<DomainException>(() =>
            AqQuery<GetAuctionReceiptQuery, AuctionCommandResult>(
                new(fixture.Admin, scenario.ActiveSessionId, scenario.RequestId)));
        Assert.Equal(404, hiddenFromAdmin.StatusCode);
    }

    [Fact]
    public async Task BidHistoryIsScopedToTheSessionAndPagedNewestFirst()
    {
        var scenario = await AqCreateScenario();

        var firstPage = await AqQuery<GetAuctionBidsQuery, AuctionPage<AuctionBidView>>(
            new(scenario.FirstUser, scenario.ActiveSessionId, scenario.CurrentAuctionId, 1, 1));
        var secondPage = await AqQuery<GetAuctionBidsQuery, AuctionPage<AuctionBidView>>(
            new(scenario.FirstUser, scenario.ActiveSessionId, scenario.CurrentAuctionId, 2, 1));

        Assert.Equal(2, firstPage.Total);
        Assert.Equal(2, Assert.Single(firstPage.Items).Sequence);
        Assert.Equal(1, Assert.Single(secondPage.Items).Sequence);

        var wrongSession = await Assert.ThrowsAsync<DomainException>(() =>
            AqQuery<GetAuctionBidsQuery, AuctionPage<AuctionBidView>>(
                new(scenario.FirstUser, scenario.CompletedSessionId, scenario.CurrentAuctionId)));
        Assert.Equal(404, wrongSession.StatusCode);
    }

    [Fact]
    public async Task RosterCoversTheWholeSeasonAndUsesTheAcquisitionListSnapshot()
    {
        var scenario = await AqCreateScenario();

        var roster = await AqQuery<GetAuctionRosterQuery, AuctionPage<AuctionRosterView>>(
            new(scenario.FirstUser, scenario.ActiveSessionId, Page: 1, PageSize: 1));
        var firstTeam = await AqQuery<GetAuctionRosterQuery, AuctionPage<AuctionRosterView>>(
            new(scenario.FirstUser, scenario.ActiveSessionId, scenario.FirstTeamId));

        Assert.Equal(2, roster.Total);
        Assert.Equal("Difensore attuale", Assert.Single(roster.Items).Name);
        var historical = Assert.Single(firstTeam.Items);
        Assert.Equal("Portiere storico", historical.Name);
        Assert.Equal("Club storico", historical.ClubName);
        Assert.Equal(scenario.HistoricalAuctionId, historical.PlayerAuctionId);
        var foreignTeam = await Assert.ThrowsAsync<DomainException>(() =>
            AqQuery<GetAuctionRosterQuery, AuctionPage<AuctionRosterView>>(
                new(scenario.FirstUser, scenario.ActiveSessionId, Guid.CreateVersion7())));
        Assert.Equal(404, foreignTeam.StatusCode);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(10001, 50)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task PagedQueriesRejectValuesOutsideTheDocumentedBounds(int page, int pageSize)
    {
        var scenario = await AqCreateScenario();

        var bids = await Assert.ThrowsAsync<DomainException>(() =>
            AqQuery<GetAuctionBidsQuery, AuctionPage<AuctionBidView>>(
                new(scenario.FirstUser, scenario.ActiveSessionId, scenario.CurrentAuctionId, page, pageSize)));
        var roster = await Assert.ThrowsAsync<DomainException>(() =>
            AqQuery<GetAuctionRosterQuery, AuctionPage<AuctionRosterView>>(
                new(scenario.FirstUser, scenario.ActiveSessionId, null, page, pageSize)));

        Assert.Equal("auction.invalid_query", bids.Code);
        Assert.Equal("auction.invalid_query", roster.Code);
    }

    [Fact]
    public async Task FilteredSessionIndexRejectsASecondLiveSessionForTheSeason()
    {
        var scenario = await AqCreateScenario();

        await AqRun(async services =>
        {
            var db = services.GetRequiredService<FantasticheDbContext>();
            db.AuctionSessions.Add(new AuctionSession
            {
                LeagueId = scenario.LeagueId,
                LeagueSeasonId = scenario.SeasonId,
                ListVersionId = scenario.CurrentListVersionId,
                Status = AuctionSessionStatus.Paused,
                CurrentPosition = 0,
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedByUserId = fixture.Admin.UserId!.Value
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }

    private async Task<AqScenario> AqCreateScenario()
    {
        var firstUser = await AqCreateUser("first");
        var secondUser = await AqCreateUser("second");
        var pendingUser = await AqCreateUser("pending");
        return await AqRun(async services =>
        {
            var db = services.GetRequiredService<FantasticheDbContext>();
            var now = DateTimeOffset.UtcNow;
            var source = "AuctionTest-" + Guid.NewGuid().ToString("N");
            var league = new League { Name = "Lega query " + Guid.NewGuid().ToString("N"), CreatedAt = now };
            var historicalList = new ListVersion
            {
                SeasonName = "2025/26",
                Status = ListVersionStatus.Published,
                Source = source,
                ContentHash = new string('a', 64),
                EntryCount = 1,
                CreatedByUserId = fixture.Admin.UserId!.Value,
                CreatedAt = now.AddDays(-10),
                PublishedAt = now.AddDays(-9)
            };
            var currentList = new ListVersion
            {
                SeasonName = "2026/27",
                Status = ListVersionStatus.Published,
                Source = source,
                ContentHash = new string('b', 64),
                EntryCount = 2,
                CreatedByUserId = fixture.Admin.UserId!.Value,
                CreatedAt = now.AddDays(-2),
                PublishedAt = now.AddDays(-1)
            };
            var season = new LeagueSeason { LeagueId = league.Id, Name = "2026/27", ListVersionId = currentList.Id };
            var emptySeason = new LeagueSeason { LeagueId = league.Id, Name = "2027/28", ListVersionId = currentList.Id };
            var firstTeam = new Team { LeagueId = league.Id, LeagueSeasonId = season.Id, Name = "Prima", NormalizedName = "PRIMA", Budget = 497 };
            var secondTeam = new Team { LeagueId = league.Id, LeagueSeasonId = season.Id, Name = "Seconda", NormalizedName = "SECONDA", Budget = 495 };
            var historicalClub = new Club { Source = source, Name = "Club storico", NormalizedName = "CLUB STORICO" };
            var currentClub = new Club { Source = source, Name = "Club attuale", NormalizedName = "CLUB ATTUALE" };
            var historicalPlayer = new Player { Source = source, ExternalId = Guid.NewGuid().ToString("N") };
            var currentPlayer = new Player { Source = source, ExternalId = Guid.NewGuid().ToString("N") };
            var completedSession = new AuctionSession
            {
                LeagueId = league.Id,
                LeagueSeasonId = season.Id,
                ListVersionId = historicalList.Id,
                Status = AuctionSessionStatus.Completed,
                CurrentPosition = 0,
                Version = 3,
                CreatedAt = now.AddHours(-2),
                CreatedByUserId = fixture.Admin.UserId!.Value
            };
            var activeSession = new AuctionSession
            {
                LeagueId = league.Id,
                LeagueSeasonId = season.Id,
                ListVersionId = currentList.Id,
                Status = AuctionSessionStatus.Active,
                CurrentPosition = 1,
                Version = 4,
                CreatedAt = now.AddHours(-1),
                CreatedByUserId = fixture.Admin.UserId!.Value
            };
            var historicalAuction = new PlayerAuction
            {
                SessionId = completedSession.Id,
                LeagueSeasonId = season.Id,
                LeagueId = league.Id,
                ListVersionId = historicalList.Id,
                PlayerId = historicalPlayer.Id,
                Number = 1,
                CallerTeamId = firstTeam.Id,
                WinningTeamId = firstTeam.Id,
                Role = "P",
                DurationSeconds = 5,
                IncrementOptionsJson = "[1]",
                CurrentAmount = 3,
                BidSequence = 1,
                Deadline = now.AddHours(-2),
                Status = PlayerAuctionStatus.Closed,
                StartedAt = now.AddHours(-2).AddSeconds(-5),
                ClosedAt = now.AddHours(-2)
            };
            var currentAuction = new PlayerAuction
            {
                SessionId = activeSession.Id,
                LeagueSeasonId = season.Id,
                LeagueId = league.Id,
                ListVersionId = currentList.Id,
                PlayerId = currentPlayer.Id,
                Number = 1,
                CallerTeamId = firstTeam.Id,
                WinningTeamId = secondTeam.Id,
                Role = "D",
                DurationSeconds = 10,
                IncrementOptionsJson = "[1,3]",
                CurrentAmount = 5,
                BidSequence = 2,
                Deadline = now.AddMinutes(-1),
                Status = PlayerAuctionStatus.Closed,
                StartedAt = now.AddMinutes(-2),
                ClosedAt = now.AddMinutes(-1)
            };
            var requestId = Guid.CreateVersion7();
            var storedResult = new AuctionCommandResult(
                requestId, activeSession.Id, currentAuction.Id, 4, now, false, 409,
                "auction.bid_too_low", "Offerta troppo bassa.");

            db.AddRange(
                league, historicalList, currentList, season, emptySeason,
                new LeagueMember { LeagueId = league.Id, UserId = firstUser.UserId!.Value, Status = MembershipStatus.Active },
                new LeagueMember { LeagueId = league.Id, UserId = secondUser.UserId!.Value, Status = MembershipStatus.Active },
                new LeagueMember { LeagueId = league.Id, UserId = pendingUser.UserId!.Value, Status = MembershipStatus.Pending },
                firstTeam, secondTeam,
                new TeamMember { TeamId = firstTeam.Id, LeagueId = league.Id, LeagueSeasonId = season.Id, UserId = firstUser.UserId.Value },
                new TeamMember { TeamId = secondTeam.Id, LeagueId = league.Id, LeagueSeasonId = season.Id, UserId = secondUser.UserId.Value },
                historicalClub, currentClub, historicalPlayer, currentPlayer,
                new ListEntry
                {
                    ListVersionId = historicalList.Id,
                    PlayerId = historicalPlayer.Id,
                    ClubId = historicalClub.Id,
                    Name = "Portiere storico",
                    FullName = "Portiere storico",
                    Role = "P",
                    ClubName = "Club storico",
                    BirthDate = new DateTime(2000, 1, 1),
                    Nationality = "Italia",
                    PreferredFoot = "Destro"
                },
                new ListEntry
                {
                    ListVersionId = currentList.Id,
                    PlayerId = historicalPlayer.Id,
                    ClubId = currentClub.Id,
                    Name = "Portiere rinominato",
                    FullName = "Portiere rinominato",
                    Role = "P",
                    ClubName = "Club attuale",
                    BirthDate = new DateTime(2000, 1, 1),
                    Nationality = "Italia",
                    PreferredFoot = "Destro"
                },
                new ListEntry
                {
                    ListVersionId = currentList.Id,
                    PlayerId = currentPlayer.Id,
                    ClubId = currentClub.Id,
                    Name = "Difensore attuale",
                    FullName = "Difensore attuale",
                    Role = "D",
                    ClubName = "Club attuale",
                    BirthDate = new DateTime(2001, 1, 1),
                    Nationality = "Italia",
                    PreferredFoot = "Sinistro"
                },
                completedSession, activeSession,
                new CallOrderEntry { SessionId = activeSession.Id, TeamId = firstTeam.Id, LeagueSeasonId = season.Id, LeagueId = league.Id, Position = 0 },
                new CallOrderEntry { SessionId = activeSession.Id, TeamId = secondTeam.Id, LeagueSeasonId = season.Id, LeagueId = league.Id, Position = 1 },
                historicalAuction, currentAuction,
                new Bid { PlayerAuctionId = currentAuction.Id, LeagueSeasonId = season.Id, LeagueId = league.Id, TeamId = firstTeam.Id, UserId = firstUser.UserId.Value, Amount = 1, Sequence = 1, AcceptedAt = now.AddSeconds(-90) },
                new Bid { PlayerAuctionId = currentAuction.Id, LeagueSeasonId = season.Id, LeagueId = league.Id, TeamId = secondTeam.Id, UserId = secondUser.UserId.Value, Amount = 5, Sequence = 2, AcceptedAt = now.AddSeconds(-80) },
                new RosterEntry { LeagueSeasonId = season.Id, LeagueId = league.Id, PlayerId = historicalPlayer.Id, TeamId = firstTeam.Id, PlayerAuctionId = historicalAuction.Id, Role = "P", Price = 3, AcquiredAt = now.AddHours(-2) },
                new RosterEntry { LeagueSeasonId = season.Id, LeagueId = league.Id, PlayerId = currentPlayer.Id, TeamId = secondTeam.Id, PlayerAuctionId = currentAuction.Id, Role = "D", Price = 5, AcquiredAt = now.AddMinutes(-1) },
                new CommandReceipt { SessionId = activeSession.Id, UserId = firstUser.UserId.Value, RequestId = requestId, CommandType = "Bid", PayloadHash = new string('c', 64), ResultJson = JsonSerializer.Serialize(storedResult, AqJson), CreatedAt = now });
            await db.SaveChangesAsync();

            return new AqScenario(
                league.Id, season.Id, emptySeason.Id, activeSession.Id, completedSession.Id,
                firstTeam.Id, secondTeam.Id, currentList.Id, historicalAuction.Id, currentAuction.Id,
                requestId, firstUser, secondUser, pendingUser, storedResult);
        });
    }

    private async Task<RequestContext> AqCreateUser(string label)
    {
        return await AqRun(async services =>
        {
            var manager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var id = Guid.CreateVersion7();
            var email = $"auction-{label}-{id:N}@example.test";
            var user = new ApplicationUser { Id = id, Email = email, UserName = email, DisplayName = label, EmailConfirmed = true };
            Assert.True((await manager.CreateAsync(user, "Auction-User-123!")).Succeeded);
            return new RequestContext(id, false, "auction-query-test");
        });
    }

    private Task<TResponse> AqQuery<TRequest, TResponse>(TRequest request)
        where TRequest : IRequest<TResponse> =>
        AqRun(services => services.GetRequiredService<IRequestPublisher>().QueryAsync<TRequest, TResponse>(request));

    private async Task<T> AqRun<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider);
    }

    private async Task AqRun(Func<IServiceProvider, Task> work)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await work(scope.ServiceProvider);
    }

    private sealed record AqScenario(
        Guid LeagueId,
        Guid SeasonId,
        Guid EmptySeasonId,
        Guid ActiveSessionId,
        Guid CompletedSessionId,
        Guid FirstTeamId,
        Guid SecondTeamId,
        Guid CurrentListVersionId,
        Guid HistoricalAuctionId,
        Guid CurrentAuctionId,
        Guid RequestId,
        RequestContext FirstUser,
        RequestContext SecondUser,
        RequestContext PendingUser,
        AuctionCommandResult StoredResult);
}
