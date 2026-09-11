using Microsoft.Extensions.Configuration;

namespace Fantastiche.Infrastructure.Common;

/// <summary>
/// Modalità di persistenza delle chiavi DataProtection, condivise da API e Scheduler.
/// Su Azure le chiavi stanno su Blob e sono cifrate con una chiave Key Vault;
/// in locale restano su file system, con certificato PFX fuori Development.
/// </summary>
public abstract record DataProtectionSettings
{
    public sealed record AzureBlob(Uri BlobUri, Uri KeyVaultKeyId) : DataProtectionSettings;

    public sealed record FileSystem(string KeyPath, string? CertificatePath, string? CertificatePassword) : DataProtectionSettings;

    public static DataProtectionSettings Resolve(IConfiguration configuration, string environment)
    {
        var blobUri = configuration["DataProtection:BlobUri"];
        if (!string.IsNullOrWhiteSpace(blobUri))
        {
            var keyId = configuration["DataProtection:KeyVaultKeyId"];
            if (string.IsNullOrWhiteSpace(keyId))
                throw new InvalidOperationException("DataProtection__BlobUri richiede anche DataProtection__KeyVaultKeyId.");
            return new AzureBlob(new Uri(blobUri, UriKind.Absolute), new Uri(keyId, UriKind.Absolute));
        }

        var keyPath = configuration["DataProtection:KeyPath"];
        var certificatePath = configuration["DataProtection:CertificatePath"];
        if (environment != "Development" && (string.IsNullOrWhiteSpace(keyPath) || string.IsNullOrWhiteSpace(certificatePath)))
            throw new InvalidOperationException(
                "Fuori Development servono DataProtection__KeyPath e DataProtection__CertificatePath persistenti, oppure DataProtection__BlobUri e DataProtection__KeyVaultKeyId.");
        return new FileSystem(keyPath ?? ".local/keys", certificatePath, configuration["DataProtection:CertificatePassword"]);
    }
}
