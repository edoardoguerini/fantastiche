using System.Text.Json;
using Fantastiche.Infrastructure.Catalog;
using Fantastiche.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fantastiche.IntegrationTests.Catalog;

public sealed class PlayerMediaTests(SqlFixture fixture) : IClassFixture<SqlFixture>
{
    [Fact]
    public async Task ManifestImportWorksBeforePlayersExistAndConcurrentReplayPreservesMissingMedia()
    {
        var file = Path.GetTempFileName();
        var externalId = Guid.NewGuid().ToString("N");
        var otherId = Guid.NewGuid().ToString("N");
        var downloadedAt = DateTimeOffset.UtcNow;
        object Card(string id, string hash) => new
        {
            externalId = id,
            sourceUrl = $"https://content.fantacalcio.it/web/campioncini/{id}.png",
            blobName = $"fantacalcio/{id}.png",
            contentType = "image/png",
            contentLength = 123,
            sha256 = hash,
            downloadedAt
        };
        try
        {
            await File.WriteAllTextAsync(file, JsonSerializer.Serialize(new
            {
                source = "FantacalcioCsv",
                items = new[] { Card(externalId, new string('a', 64)), Card(otherId, new string('b', 64)) }
            }));
            var first = await Import();
            Assert.Equal(2, first.Inserted);
            var replays = await Task.WhenAll(Import(), Import());
            Assert.All(replays, replay =>
            {
                Assert.Equal(0, replay.Inserted);
                Assert.Equal(0, replay.Updated);
                Assert.Equal(2, replay.Unchanged);
            });
            await File.WriteAllTextAsync(file, JsonSerializer.Serialize(new
            {
                source = "FantacalcioCsv",
                items = new[] { Card(externalId, new string('c', 64)), Card("invalid", "bad-hash") }
            }));
            var changed = await Import();
            Assert.Equal(1, changed.Updated);
            Assert.Equal(1, changed.Skipped);
            await using var scope = fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FantasticheDbContext>();
            Assert.False(await db.Players.AnyAsync(x => x.ExternalId == externalId || x.ExternalId == otherId));
            var media = await db.PlayerMedia.Where(x => x.ExternalId == externalId || x.ExternalId == otherId).ToListAsync();
            Assert.Equal(2, media.Count);
            Assert.Equal(new string('c', 64), Assert.Single(media, x => x.ExternalId == externalId).Sha256);
            Assert.Equal(new string('b', 64), Assert.Single(media, x => x.ExternalId == otherId).Sha256);
        }
        finally
        {
            File.Delete(file);
        }

        async Task<PlayerMediaImportResult> Import()
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<PlayerMediaImporter>().ImportAsync(file);
        }
    }
}
