using Microsoft.Extensions.Configuration;

namespace Fantastiche.Infrastructure.Leagues;

public sealed class LeagueLogoStorage
{
    private readonly string? baseUrl;

    public LeagueLogoStorage(IConfiguration configuration)
    {
        var value = configuration["Storage:LeagueLogos:PublicBaseUrl"]?.Trim();
        if (string.IsNullOrEmpty(value)) return;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new InvalidOperationException("Storage:LeagueLogos:PublicBaseUrl deve essere un URL HTTP(S) pubblico senza credenziali, query o frammento.");
        baseUrl = uri.AbsoluteUri.TrimEnd('/');
    }

    public string? Url(string? blobName) => baseUrl is null || string.IsNullOrWhiteSpace(blobName)
        ? null
        : $"{baseUrl}/{string.Join('/', blobName.Split('/').Select(Uri.EscapeDataString))}";
}
