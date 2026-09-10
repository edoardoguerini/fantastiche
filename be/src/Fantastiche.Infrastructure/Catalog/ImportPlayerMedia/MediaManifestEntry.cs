using System.Text.Json;
using System.Text.RegularExpressions;

namespace Fantastiche.Infrastructure.Catalog;

internal sealed record MediaManifestEntry(string SourceUrl, string BlobName, long ContentLength, string Sha256, DateTimeOffset DownloadedAt)
{
    internal static MediaManifestEntry? Parse(JsonElement item)
    {
        var sourceUrl = Text(item, "sourceUrl");
        var blobName = Text(item, "blobName");
        var sha256 = Text(item, "sha256");
        if (sourceUrl is null || sourceUrl.Length > 2048
            || !Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https"
            || uri.Host != "content.fantacalcio.it" || uri.UserInfo.Length != 0 || !uri.IsDefaultPort
            || uri.Fragment.Length != 0
            || blobName is null || blobName.Length > 512
            || !Regex.IsMatch(blobName, @"\A[A-Za-z0-9_-]+(?:/[A-Za-z0-9_-]+)*\.png\z", RegexOptions.CultureInvariant)
            || Text(item, "contentType") != "image/png" || sha256 is null
            || !Regex.IsMatch(sha256, @"\A[0-9a-fA-F]{64}\z", RegexOptions.CultureInvariant)
            || !item.TryGetProperty("contentLength", out var lengthElement) || lengthElement.ValueKind != JsonValueKind.Number
            || !lengthElement.TryGetInt64(out var length) || length is <= 0 or > 10485760
            || !item.TryGetProperty("downloadedAt", out var dateElement) || dateElement.ValueKind != JsonValueKind.String
            || !dateElement.TryGetDateTimeOffset(out var downloadedAt) || downloadedAt == default)
            return null;
        return new(sourceUrl, blobName, length, sha256.ToLowerInvariant(), downloadedAt.ToUniversalTime());
    }

    internal static string? Text(JsonElement item, string name) =>
        item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;

    internal static bool Identifier(string? value, int maximum) => value is { Length: > 0 } && value.Length <= maximum
        && Regex.IsMatch(value, @"\A[A-Za-z0-9_-]+\z", RegexOptions.CultureInvariant);
}
