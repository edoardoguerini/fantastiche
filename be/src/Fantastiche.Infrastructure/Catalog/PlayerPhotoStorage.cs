using Microsoft.Extensions.Configuration;

namespace Fantastiche.Infrastructure.Catalog;

public sealed class PlayerPhotoStorage
{
    internal const string SqlProjection = "CASE WHEN @PhotoBaseUrl IS NULL THEN NULL ELSE @PhotoBaseUrl + N'/' + media.BlobName END AS PhotoUrl";

    public PlayerPhotoStorage(IConfiguration configuration) => PublicBaseUrl = ReadBaseUrl(configuration, "Storage:PlayerPhotos:PublicBaseUrl");

    internal static string? ReadBaseUrl(IConfiguration configuration, string key)
    {
        var configured = configuration[key]?.Trim();
        if (string.IsNullOrEmpty(configured)) return null;
        if (!Uri.TryCreate(configured, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new InvalidOperationException($"{key} deve essere un URL HTTP(S) pubblico senza credenziali, query o frammento.");
        return uri.AbsoluteUri.TrimEnd('/');
    }

    public string? PublicBaseUrl { get; }
}
