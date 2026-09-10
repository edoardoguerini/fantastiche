using System.Data;
using Fantastiche.Core.Exceptions;
using Fantastiche.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fantastiche.Infrastructure.Catalog;

public sealed record ClubMediaImportResult(int Inserted, int Updated, int Unchanged, int Skipped);

public sealed class ClubMediaImporter(FantasticheDbContext db)
{
    public async Task<ClubMediaImportResult> ImportAsync(string manifestPath, CancellationToken ct = default)
    {
        // Lettura e validazione precedono l'apertura della transazione SQL.
        var file = new FileInfo(manifestPath);
        if (file.Length > 10485760)
            throw new DomainException("catalog.invalid_media_manifest", "Il manifest delle immagini supera 10 MiB.");
        var manifest = ClubMediaManifest.Parse(await File.ReadAllTextAsync(manifestPath, ct));
        if (manifest.Items.Count == 0) return new(0, 0, 0, manifest.Skipped);

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource = N'Fantastiche:ClubMediaImport',
                @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
            IF @result < 0 THROW 51000, 'Club media import busy', 1;
            """, ct);
        var ids = manifest.Items.Select(x => x.NormalizedClubName).ToArray();
        var existing = (await db.ClubMedia.Where(x => x.Source == manifest.Source && ids.Contains(x.NormalizedClubName)).ToListAsync(ct))
            .ToDictionary(x => x.NormalizedClubName, StringComparer.OrdinalIgnoreCase);
        var inserted = 0;
        var updated = 0;
        var unchanged = 0;
        foreach (var media in manifest.Items)
        {
            if (!existing.TryGetValue(media.NormalizedClubName, out var current))
            {
                db.ClubMedia.Add(media);
                inserted++;
            }
            else if (current.SourceUrl == media.SourceUrl && current.BlobName == media.BlobName
                     && current.ContentType == media.ContentType && current.ContentLength == media.ContentLength
                     && current.Sha256 == media.Sha256 && current.DownloadedAt == media.DownloadedAt)
            {
                unchanged++;
            }
            else
            {
                current.SourceUrl = media.SourceUrl;
                current.BlobName = media.BlobName;
                current.ContentType = media.ContentType;
                current.ContentLength = media.ContentLength;
                current.Sha256 = media.Sha256;
                current.DownloadedAt = media.DownloadedAt;
                updated++;
            }
        }
        if (inserted + updated > 0) await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(inserted, updated, unchanged, manifest.Skipped);
    }
}
