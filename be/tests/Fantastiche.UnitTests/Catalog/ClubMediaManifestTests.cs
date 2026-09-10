using System.Text.Json;
using Fantastiche.Infrastructure.Catalog;
using Microsoft.Extensions.Configuration;

namespace Fantastiche.UnitTests.Catalog;

public sealed class ClubMediaManifestTests
{
    [Fact]
    public void ClubManifestNormalizesNamesAndSkipsInvalidOrDuplicateLogos()
    {
        var valid = new
        {
            clubName = " Atalanta ",
            sourceUrl = "https://content.fantacalcio.it/web/loghi/atalanta.png?v=1",
            blobName = "clubs/atalanta.png",
            contentType = "image/png",
            contentLength = 123,
            sha256 = new string('b', 64),
            downloadedAt = "2026-09-10T12:00:00Z"
        };
        var manifest = ClubMediaManifest.Parse(JsonSerializer.Serialize(new
        {
            source = "FantacalcioCsv",
            items = new[] { valid, valid with { clubName = "ATALANTA" }, valid with { clubName = " " }, valid with { clubName = "Roma", blobName = "../roma.png" } }
        }));
        var logo = Assert.Single(manifest.Items);
        Assert.Equal("ATALANTA", logo.NormalizedClubName);
        Assert.Equal("FantacalcioCsv", logo.Source);
        Assert.Equal(3, manifest.Skipped);
    }

    [Fact]
    public void ClubLogoStorageHasItsOwnOptionalBrowserBaseUrl()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:PlayerPhotos:PublicBaseUrl"] = "http://localhost:10010/fantastiche/player-photos"
        }).Build();
        Assert.Null(new ClubLogoStorage(configuration).PublicBaseUrl);
        configuration["Storage:ClubLogos:PublicBaseUrl"] = "http://localhost:10010/fantastiche/club-logos/";
        Assert.Equal("http://localhost:10010/fantastiche/club-logos", new ClubLogoStorage(configuration).PublicBaseUrl);
    }
}
