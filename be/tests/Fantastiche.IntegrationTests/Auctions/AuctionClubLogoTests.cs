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
    public async Task ClubLogosFollowTheListEntryClubSnapshotAndRequireMatchingSourceAndConfiguration()
    {
        var s = await AqCreateScenario();
        var configuration = fixture.Services.GetRequiredService<IConfiguration>();
        await AqRun(async services =>
        {
            var db = services.GetRequiredService<FantasticheDbContext>();
            var clubId = await db.ListEntries.Where(x => x.ListVersionId == s.CurrentListVersionId).Select(x => x.ClubId).FirstAsync();
            var club = await db.Clubs.SingleAsync(x => x.Id == clubId);
            db.ClubMedia.AddRange(Logo(club.Source, club.NormalizedName, "clubs/current.png"),
                Logo("WrongSource", "CLUB STORICO", "clubs/wrong.png"));
            await db.SaveChangesAsync();
        });
        Assert.Null((await AqQuery<GetAuctionStateQuery, AuctionSessionView>(new(s.FirstUser, s.ActiveSessionId))).CurrentAuction!.ClubLogoUrl);
        configuration["Storage:ClubLogos:PublicBaseUrl"] = "http://localhost:10010/fantastiche/club-logos/";
        try
        {
            const string expected = "http://localhost:10010/fantastiche/club-logos/clubs/current.png";
            var state = await AqQuery<GetAuctionStateQuery, AuctionSessionView>(new(s.FirstUser, s.ActiveSessionId));
            var active = await AqQuery<GetActiveAuctionQuery, AuctionSessionView?>(new(s.FirstUser, s.LeagueId, s.SeasonId));
            Assert.Equal(expected, state.CurrentAuction!.ClubLogoUrl);
            Assert.Equal(expected, active!.CurrentAuction!.ClubLogoUrl);
            var catalog = await AqQuery<GetAuctionCatalogQuery, AuctionPage<AuctionCatalogPlayerView>>(new(s.FirstUser, s.ActiveSessionId, AvailableOnly: false));
            Assert.All(catalog.Items, x => Assert.Equal(expected, x.ClubLogoUrl));
            var global = await AqQuery<GetCatalogEntriesQuery, CatalogPage<CatalogEntryView>>(new(s.FirstUser, s.CurrentListVersionId));
            Assert.All(global.Items, x => Assert.Equal(expected, x.ClubLogoUrl));
            var roster = await AqQuery<GetAuctionRosterQuery, AuctionPage<AuctionRosterView>>(new(s.FirstUser, s.ActiveSessionId));
            Assert.Equal(expected, Assert.Single(roster.Items, x => x.TeamId == s.SecondTeamId).ClubLogoUrl);
            Assert.Null(Assert.Single(roster.Items, x => x.TeamId == s.FirstTeamId).ClubLogoUrl);
        }
        finally
        {
            configuration["Storage:ClubLogos:PublicBaseUrl"] = null;
        }

        static ClubMedia Logo(string source, string name, string blobName) => new()
        {
            Source = source,
            NormalizedClubName = name,
            BlobName = blobName,
            SourceUrl = "https://content.fantacalcio.it/web/loghi/logo.png",
            ContentType = "image/png",
            ContentLength = 123,
            Sha256 = new string('a', 64),
            DownloadedAt = DateTimeOffset.UtcNow
        };
    }
}
