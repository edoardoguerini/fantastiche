using Fantastiche.Infrastructure.Common;
using Microsoft.Extensions.Configuration;

namespace Fantastiche.UnitTests.Common;

public sealed class DataProtectionSettingsTests
{
    private static IConfiguration Build(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value))
            .Build();

    [Fact]
    public void Resolve_ConBlobUriEKeyId_SceglieAzure()
    {
        var configuration = Build(
            ("DataProtection:BlobUri", "https://stfantasticheprod.blob.core.windows.net/dataprotection/keys.xml"),
            ("DataProtection:KeyVaultKeyId", "https://kv-fantastiche-prod.vault.azure.net/keys/dataprotection"));

        var settings = DataProtectionSettings.Resolve(configuration, "Production");

        var azure = Assert.IsType<DataProtectionSettings.AzureBlob>(settings);
        Assert.Equal("https://stfantasticheprod.blob.core.windows.net/dataprotection/keys.xml", azure.BlobUri.ToString());
        Assert.Equal("https://kv-fantastiche-prod.vault.azure.net/keys/dataprotection", azure.KeyVaultKeyId.ToString());
    }

    [Fact]
    public void Resolve_ConBlobUriSenzaKeyId_Fallisce()
    {
        var configuration = Build(("DataProtection:BlobUri", "https://stfantasticheprod.blob.core.windows.net/dataprotection/keys.xml"));

        var exception = Assert.Throws<InvalidOperationException>(() => DataProtectionSettings.Resolve(configuration, "Production"));

        Assert.Contains("DataProtection__KeyVaultKeyId", exception.Message);
    }

    [Fact]
    public void Resolve_InProductionSenzaPercorsi_Fallisce()
    {
        var configuration = Build();

        Assert.Throws<InvalidOperationException>(() => DataProtectionSettings.Resolve(configuration, "Production"));
    }

    [Fact]
    public void Resolve_InDevelopmentSenzaConfigurazione_UsaFileSystemDiDefault()
    {
        var configuration = Build();

        var settings = DataProtectionSettings.Resolve(configuration, "Development");

        var file = Assert.IsType<DataProtectionSettings.FileSystem>(settings);
        Assert.Equal(".local/keys", file.KeyPath);
        Assert.Null(file.CertificatePath);
    }

    [Fact]
    public void Resolve_ConPercorsiEspliciti_UsaFileSystemConCertificato()
    {
        var configuration = Build(
            ("DataProtection:KeyPath", "/app/.local/keys"),
            ("DataProtection:CertificatePath", "/app/.local/cert.pfx"),
            ("DataProtection:CertificatePassword", "segreto"));

        var settings = DataProtectionSettings.Resolve(configuration, "Production");

        var file = Assert.IsType<DataProtectionSettings.FileSystem>(settings);
        Assert.Equal("/app/.local/keys", file.KeyPath);
        Assert.Equal("/app/.local/cert.pfx", file.CertificatePath);
        Assert.Equal("segreto", file.CertificatePassword);
    }
}
