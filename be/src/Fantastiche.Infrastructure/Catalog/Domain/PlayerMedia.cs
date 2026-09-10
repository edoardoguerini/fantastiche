namespace Fantastiche.Infrastructure.Catalog;

public sealed class PlayerMedia
{
    public string Source { get; set; } = "";
    public string ExternalId { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public string BlobName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long ContentLength { get; set; }
    public string Sha256 { get; set; } = "";
    public DateTimeOffset DownloadedAt { get; set; }
}
