using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Fantastiche.Core.Exceptions;
using Fantastiche.Core.Storage;
using Microsoft.Extensions.Configuration;

namespace Fantastiche.Gateways.AzureBlob;

public sealed class AzureLeagueLogoStore(IConfiguration configuration) : ILeagueLogoStore
{
    private BlobContainerClient Container()
    {
        var connection = configuration["Storage:LeagueLogos:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(connection))
            return new BlobContainerClient(connection, "league-logos");
        var url = configuration["Storage:LeagueLogos:PublicBaseUrl"];
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https")
            return new BlobContainerClient(uri, new DefaultAzureCredential());
        throw Unavailable();
    }

    public async Task UploadAsync(string blobName, byte[] content, string contentType, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var container = Container();
            // Solo Azurite locale può inizializzare il container. Azure è gestito da Bicep.
            var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? configuration["DOTNET_ENVIRONMENT"];
            if (environment == "Development")
                await container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: timeout.Token);
            await container.GetBlobClient(blobName).UploadAsync(BinaryData.FromBytes(content), new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType,
                    ContentDisposition = "inline",
                    CacheControl = "public, max-age=31536000, immutable"
                },
                Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }
            }, timeout.Token);
        }
        catch (RequestFailedException) { throw Unavailable(); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw Unavailable(); }
    }

    public async Task DeleteAsync(string blobName, CancellationToken ct) =>
        await Container().GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: ct);

    private static DomainException Unavailable() => new("league.logo_unavailable",
        "Non è stato possibile salvare il logo. Riprova oppure rimuovilo per creare la lega.", 503);
}
