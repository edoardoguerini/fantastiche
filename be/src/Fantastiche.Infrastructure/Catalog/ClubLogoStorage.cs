using Microsoft.Extensions.Configuration;

namespace Fantastiche.Infrastructure.Catalog;

public sealed class ClubLogoStorage(IConfiguration configuration)
{
    internal const string SqlProjection = "CASE WHEN @ClubLogoBaseUrl IS NULL THEN NULL ELSE @ClubLogoBaseUrl + N'/' + clubMedia.BlobName END AS ClubLogoUrl";
    public string? PublicBaseUrl { get; } = PlayerPhotoStorage.ReadBaseUrl(configuration, "Storage:ClubLogos:PublicBaseUrl");
}
