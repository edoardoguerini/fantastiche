using Fantastiche.Infrastructure.Auctions;
using Fantastiche.Infrastructure.Catalog;
using Fantastiche.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fantastiche.IntegrationTests.Auctions;

public sealed partial class AuctionQueryTests
{
    [Fact]
    public async Task PhotoUrlsRequireConfiguredStorageAndExactSourceIdentityAcrossCatalogStateAndRoster()
    {
        var s = await AqCreateScenario();
        var configuration = fixture.Services.GetRequiredService<IConfiguration>();
        await AqRun(async services =>
        {
            var db = services.GetRequiredService<FantasticheDbContext>();
            var playerId = await db.PlayerAuctions.Where(x => x.Id == s.CurrentAuctionId).Select(x => x.PlayerId).SingleAsync();
            var historicalId = await db.PlayerAuctions.Where(x => x.Id == s.HistoricalAuctionId).Select(x => x.PlayerId).SingleAsync();
            var player = await db.Players.SingleAsync(x => x.Id == playerId);
            var historical = await db.Players.SingleAsync(x => x.Id == historicalId);
            db.PlayerMedia.AddRange(
                Card(player.Source, player.ExternalId, "fantacalcio/current.png"),
                Card("DifferentSource", historical.ExternalId, "fantacalcio/wrong.png"));
            await db.SaveChangesAsync();
        });
        var noStorage = await AqQuery<GetAuctionStateQuery, AuctionSessionView>(new(s.FirstUser, s.ActiveSessionId));
        Assert.Null(noStorage.CurrentAuction!.PhotoUrl);
        configuration["Storage:PlayerPhotos:PublicBaseUrl"] = "http://localhost:10010/fantastiche/player-photos/";
        try
        {
            const string expected = "http://localhost:10010/fantastiche/player-photos/fantacalcio/current.png";
            var state = await AqQuery<GetAuctionStateQuery, AuctionSessionView>(new(s.FirstUser, s.ActiveSessionId));
            var active = await AqQuery<GetActiveAuctionQuery, AuctionSessionView?>(new(s.FirstUser, s.LeagueId, s.SeasonId));
            Assert.Equal(expected, state.CurrentAuction!.PhotoUrl);
            Assert.Equal(expected, active!.CurrentAuction!.PhotoUrl);
            var catalog = await AqQuery<GetAuctionCatalogQuery, AuctionPage<AuctionCatalogPlayerView>>(new(s.FirstUser, s.ActiveSessionId, AvailableOnly: false));
            Assert.Equal(expected, Assert.Single(catalog.Items, x => x.Name == "Difensore attuale").PhotoUrl);
            Assert.Null(Assert.Single(catalog.Items, x => x.Name == "Portiere rinominato").PhotoUrl);
            var global = await AqQuery<GetCatalogEntriesQuery, CatalogPage<CatalogEntryView>>(new(s.FirstUser, s.CurrentListVersionId));
            Assert.Equal(expected, Assert.Single(global.Items, x => x.Name == "Difensore attuale").PhotoUrl);
            Assert.Null(Assert.Single(global.Items, x => x.Name == "Portiere rinominato").PhotoUrl);
            var roster = await AqQuery<GetAuctionRosterQuery, AuctionPage<AuctionRosterView>>(new(s.FirstUser, s.ActiveSessionId));
            Assert.Equal(expected, Assert.Single(roster.Items, x => x.TeamId == s.SecondTeamId).PhotoUrl);
            Assert.Null(Assert.Single(roster.Items, x => x.TeamId == s.FirstTeamId).PhotoUrl);
        }
        finally
        {
            configuration["Storage:PlayerPhotos:PublicBaseUrl"] = null;
        }

        static PlayerMedia Card(string source, string externalId, string blobName) => new()
        {
            Source = source,
            ExternalId = externalId,
            BlobName = blobName,
            SourceUrl = "https://content.fantacalcio.it/web/campioncini/card.png",
            ContentType = "image/png",
            ContentLength = 123,
            Sha256 = new string('a', 64),
            DownloadedAt = DateTimeOffset.UtcNow
        };
    }
}
