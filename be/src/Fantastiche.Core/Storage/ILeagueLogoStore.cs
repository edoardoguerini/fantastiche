namespace Fantastiche.Core.Storage;

public interface ILeagueLogoStore
{
    Task UploadAsync(string blobName, byte[] content, string contentType, CancellationToken ct);
    Task DeleteAsync(string blobName, CancellationToken ct);
}
