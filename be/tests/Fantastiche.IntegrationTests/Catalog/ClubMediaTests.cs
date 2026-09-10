using System.Text.Json;
using Fantastiche.Infrastructure.Catalog;
using Fantastiche.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fantastiche.IntegrationTests.Catalog;

public sealed class ClubMediaTests(SqlFixture fixture) : IClassFixture<SqlFixture>
{
    [Fact]
    public async Task ClubImportIsIdempotentBeforeCatalogExistsAndRetainsAbsentLogos()
    {
        var path = Path.GetTempFileName();
        var source = "Test-" + Guid.NewGuid().ToString("N");
        var first = new
        {
            clubName = " Atalanta ",
            sourceUrl = "https://content.fantacalcio.it/web/loghi/atalanta.png",
            blobName = "clubs/atalanta.png",
            contentType = "image/png",
            contentLength = 123,
            sha256 = new string('b', 64),
            downloadedAt = DateTimeOffset.UtcNow
        };
        try
        {
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new { source, items = new[] { first, first with { clubName = "Roma", blobName = "clubs/roma.png" } } }));
            Assert.Equal(2, (await Import()).Inserted);
            var replay = await Task.WhenAll(Import(), Import());
            Assert.All(replay, x => Assert.Equal(2, x.Unchanged));
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new { source, items = new[] { first with { clubName = "ATALANTA", sha256 = new string('c', 64) } } }));
            Assert.Equal(1, (await Import()).Updated);
            await using var scope = fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FantasticheDbContext>();
            Assert.False(await db.Clubs.AnyAsync(x => x.Source == source));
            var logos = await db.ClubMedia.Where(x => x.Source == source).ToListAsync();
            Assert.Equal(2, logos.Count);
            Assert.Equal(new string('c', 64), Assert.Single(logos, x => x.NormalizedClubName == "ATALANTA").Sha256);
            Assert.Equal("clubs/roma.png", Assert.Single(logos, x => x.NormalizedClubName == "ROMA").BlobName);
        }
        finally
        {
            File.Delete(path);
        }

        async Task<ClubMediaImportResult> Import()
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ClubMediaImporter>().ImportAsync(path);
        }
    }
}
