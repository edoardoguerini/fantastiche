using System.Text.Json;
using Fantastiche.Core.Exceptions;
using static Fantastiche.Infrastructure.Catalog.MediaManifestEntry;

namespace Fantastiche.Infrastructure.Catalog;

public sealed record PlayerMediaManifest(string Source, IReadOnlyList<PlayerMedia> Items, int Skipped)
{
    public static PlayerMediaManifest Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var source = Text(root, "source");
            if (!Identifier(source, 50) || !root.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array || items.GetArrayLength() > 10000)
                throw InvalidManifest();
            var accepted = new Dictionary<string, PlayerMedia>(StringComparer.OrdinalIgnoreCase);
            var skipped = 0;
            foreach (var item in items.EnumerateArray())
            {
                var media = ParseItem(source!, item);
                if (media is null || !accepted.TryAdd(media.ExternalId, media)) skipped++;
            }
            return new(source!, accepted.Values.ToList(), skipped);
        }
        catch (JsonException)
        {
            throw InvalidManifest();
        }
    }

    private static PlayerMedia? ParseItem(string source, JsonElement item)
    {
        var externalId = Text(item, "externalId");
        var metadata = MediaManifestEntry.Parse(item);
        if (!Identifier(externalId, 32) || metadata is null) return null;
        return new PlayerMedia
        {
            Source = source,
            ExternalId = externalId!,
            SourceUrl = metadata.SourceUrl,
            BlobName = metadata.BlobName,
            ContentType = "image/png",
            ContentLength = metadata.ContentLength,
            Sha256 = metadata.Sha256,
            DownloadedAt = metadata.DownloadedAt
        };
    }

    private static DomainException InvalidManifest() => new("catalog.invalid_media_manifest", "Manifest delle immagini non valido.");
}
