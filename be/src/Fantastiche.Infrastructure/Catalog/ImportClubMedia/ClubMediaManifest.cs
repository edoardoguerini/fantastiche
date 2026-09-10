using System.Text.Json;
using Fantastiche.Core.Exceptions;
using static Fantastiche.Infrastructure.Catalog.MediaManifestEntry;

namespace Fantastiche.Infrastructure.Catalog;

public sealed record ClubMediaManifest(string Source, IReadOnlyList<ClubMedia> Items, int Skipped)
{
    public static ClubMediaManifest Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var source = Text(root, "source");
            if (!Identifier(source, 50) || !root.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array || items.GetArrayLength() > 10000)
                throw InvalidManifest();
            var accepted = new Dictionary<string, ClubMedia>(StringComparer.OrdinalIgnoreCase);
            var skipped = 0;
            foreach (var item in items.EnumerateArray())
            {
                var name = Text(item, "clubName");
                var normalizedName = name is null ? null : CatalogWorkflow.NormalizeClubName(name);
                var metadata = MediaManifestEntry.Parse(item);
                if (normalizedName is not { Length: > 0 and <= 100 } || normalizedName.Any(char.IsControl) || metadata is null)
                {
                    skipped++;
                    continue;
                }
                var media = new ClubMedia
                {
                    Source = source!,
                    NormalizedClubName = normalizedName,
                    SourceUrl = metadata.SourceUrl,
                    BlobName = metadata.BlobName,
                    ContentType = "image/png",
                    ContentLength = metadata.ContentLength,
                    Sha256 = metadata.Sha256,
                    DownloadedAt = metadata.DownloadedAt
                };
                if (!accepted.TryAdd(normalizedName, media)) skipped++;
            }
            return new(source!, accepted.Values.ToList(), skipped);
        }
        catch (JsonException)
        {
            throw InvalidManifest();
        }
    }

    private static DomainException InvalidManifest() => new("catalog.invalid_media_manifest", "Manifest dei loghi club non valido.");
}
