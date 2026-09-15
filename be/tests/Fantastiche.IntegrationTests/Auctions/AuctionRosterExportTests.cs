using Fantastiche.Core.Auth;
using Fantastiche.Core.Exceptions;
using Fantastiche.Infrastructure.Auctions;
using Fantastiche.Infrastructure.Catalog;
using Fantastiche.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fantastiche.IntegrationTests.Auctions;

public sealed partial class AuctionQueryTests
{
    private static int exportExternalId = 10_000_000;
    [Fact]
    public async Task RosterExportIncludesEverySeasonPurchaseAcrossSessionsWithoutPagination()
    {
        var scenario = await AqCreateScenario();
        var other = await AqCreateScenario();
        await AqRun(async services =>
        {
            var db = services.GetRequiredService<FantasticheDbContext>();
            var originalIds = await db.RosterEntries.Where(x => x.LeagueSeasonId == scenario.SeasonId).Select(x => x.PlayerId).ToListAsync();
            foreach (var player in await db.Players.Where(x => originalIds.Contains(x.Id)).ToListAsync())
            {
                player.Source = "FantacalcioCsv";
                player.ExternalId = Interlocked.Increment(ref exportExternalId).ToString();
            }
            var entry = await db.ListEntries.FirstAsync(x => x.ListVersionId == scenario.CurrentListVersionId);
            for (var i = 0; i < 101; i++)
            {
                var player = new Player { Source = "FantacalcioCsv", ExternalId = Interlocked.Increment(ref exportExternalId).ToString() };
                var auction = new PlayerAuction
                {
                    SessionId = scenario.ActiveSessionId,
                    LeagueId = scenario.LeagueId,
                    LeagueSeasonId = scenario.SeasonId,
                    ListVersionId = scenario.CurrentListVersionId,
                    PlayerId = player.Id,
                    Number = i + 2,
                    CallerTeamId = scenario.FirstTeamId,
                    WinningTeamId = scenario.FirstTeamId,
                    Role = "D",
                    DurationSeconds = 5,
                    IncrementOptionsJson = "[1]",
                    CurrentAmount = 1,
                    BidSequence = 1,
                    Deadline = DateTimeOffset.UtcNow,
                    Status = PlayerAuctionStatus.Closed,
                    StartedAt = DateTimeOffset.UtcNow
                };
                db.AddRange(player, new ListEntry
                {
                    ListVersionId = entry.ListVersionId,
                    PlayerId = player.Id,
                    ClubId = entry.ClubId,
                    Name = $"Giocatore {i}",
                    FullName = $"Giocatore {i}",
                    ClubName = entry.ClubName,
                    Role = "D"
                }, auction, new RosterEntry
                {
                    LeagueId = scenario.LeagueId,
                    LeagueSeasonId = scenario.SeasonId,
                    TeamId = scenario.FirstTeamId,
                    PlayerId = player.Id,
                    PlayerAuctionId = auction.Id,
                    Role = "D",
                    Price = 1,
                    AcquiredAt = DateTimeOffset.UtcNow
                });
            }
            await db.SaveChangesAsync();
        });

        var export = await AqQuery<ExportAuctionRosterQuery, AuctionRosterExportView>(new(scenario.FirstUser, scenario.ActiveSessionId));
        var lines = export.Csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(103, lines.Count(x => x != "$,$,$"));
        Assert.Equal(2, lines.Count(x => x == "$,$,$"));
        Assert.Contains(lines, x => x.StartsWith("Prima,") && x.EndsWith(",3"));
        Assert.Contains(lines, x => x.StartsWith("Seconda,") && x.EndsWith(",5"));
        Assert.Equal($"fantastiche-rosters-{scenario.SeasonId:N}.csv", export.FileName);
        Assert.Equal(export, await AqQuery<ExportAuctionRosterQuery, AuctionRosterExportView>(new(fixture.Admin, scenario.CompletedSessionId)));
        Assert.Equal(403, (await Assert.ThrowsAsync<DomainException>(() =>
            AqQuery<ExportAuctionRosterQuery, AuctionRosterExportView>(new(other.FirstUser, scenario.ActiveSessionId)))).StatusCode);
    }

    [Fact]
    public async Task RosterExportAuthorizesBeforeValidatingTheFileAndRejectsUnsupportedSources()
    {
        var scenario = await AqCreateScenario();
        foreach (var context in new[] { scenario.PendingUser, scenario.PendingUser with { IsSuperAdmin = true } })
            Assert.Equal(403, (await Assert.ThrowsAsync<DomainException>(() =>
                AqQuery<ExportAuctionRosterQuery, AuctionRosterExportView>(new(context, scenario.ActiveSessionId)))).StatusCode);
        Assert.Equal(401, (await Assert.ThrowsAsync<DomainException>(() =>
            AqQuery<ExportAuctionRosterQuery, AuctionRosterExportView>(new(new RequestContext(null, false, "test"), scenario.ActiveSessionId)))).StatusCode);
        Assert.Equal("auction.export_invalid_player", (await Assert.ThrowsAsync<DomainException>(() =>
            AqQuery<ExportAuctionRosterQuery, AuctionRosterExportView>(new(scenario.FirstUser, scenario.ActiveSessionId)))).Code);
        Assert.Equal(404, (await Assert.ThrowsAsync<DomainException>(() =>
            AqQuery<ExportAuctionRosterQuery, AuctionRosterExportView>(new(fixture.Admin, Guid.NewGuid())))).StatusCode);
    }
}
