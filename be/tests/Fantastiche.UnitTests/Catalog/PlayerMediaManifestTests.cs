using System.Text.Json;
using Fantastiche.Core.Exceptions;
using Fantastiche.Infrastructure.Catalog;
using Microsoft.Extensions.Configuration;

namespace Fantastiche.UnitTests.Catalog;

public sealed class PlayerMediaManifestTests
{
    [Fact]
    public void ManifestKeepsValidCardsAndSkipsInvalidMetadataAndDuplicateIdentities()
    {
        var valid = new
        {
            externalId = "4431",
            sourceUrl = "https://content.fantacalcio.it/web/campioncini/4431.png?v=817",
            blobName = "fantacalcio/4431.png",
            contentType = "image/png",
            contentLength = 123,
            sha256 = new string('a', 64),
            downloadedAt = "2026-09-10T12:00:00Z"
        };
        var items = new object[]
        {
            valid, valid,
            valid with { externalId = "2", blobName = "../escape.png" },
            valid with { externalId = "3", sourceUrl = "https://example.test/card.png" },
            valid with { externalId = "4", sha256 = "invalid" },
            valid with { externalId = "5", contentLength = 0 },
            valid with { externalId = "6", contentType = "text/html" },
            valid with { externalId = "7", downloadedAt = "not-a-date" },
            valid with { externalId = "8", blobName = "fantacalcio/%2e%2e/8.png" },
            valid with { externalId = "9", sourceUrl = "https://content.fantacalcio.it@evil.test/9.png" },
            new { externalId = "10" }
        };
        var parsed = PlayerMediaManifest.Parse(JsonSerializer.Serialize(new { source = "FantacalcioCsv", items }));
        var card = Assert.Single(parsed.Items);
        Assert.Equal("4431", card.ExternalId);
        Assert.Equal("FantacalcioCsv", card.Source);
        Assert.Equal("fantacalcio/4431.png", card.BlobName);
        Assert.Equal(TimeSpan.Zero, card.DownloadedAt.Offset);
        Assert.Equal(10, parsed.Skipped);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"source\":\"\",\"items\":[]}")]
    [InlineData("{\"source\":\"FantacalcioCsv\",\"items\":{}}")]
    [InlineData("not json")]
    public void InvalidEnvelopeIsRejected(string json)
        => Assert.Throws<DomainException>(() => PlayerMediaManifest.Parse(json));

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("http://localhost:10010/fantastiche/player-photos/", "http://localhost:10010/fantastiche/player-photos")]
    [InlineData("https://images.example.test/player-photos", "https://images.example.test/player-photos")]
    public void PhotoBaseUrlIsOptionalAndNormalizesTheTrailingSlash(string? configured, string? expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:PlayerPhotos:PublicBaseUrl"] = configured
        }).Build();
        Assert.Equal(expected, new PlayerPhotoStorage(configuration).PublicBaseUrl);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://example.test/photos?token=secret")]
    [InlineData("https://user:password@example.test/photos")]
    public void PhotoBaseUrlRejectsExecutableOrCredentialBearingUrls(string configured)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:PlayerPhotos:PublicBaseUrl"] = configured
        }).Build();
        Assert.Throws<InvalidOperationException>(() => new PlayerPhotoStorage(configuration));
    }
}
