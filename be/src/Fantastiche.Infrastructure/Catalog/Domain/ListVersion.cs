namespace Fantastiche.Infrastructure.Catalog;

public enum ListVersionStatus
{
    Draft,
    Published
}

public sealed class ListVersion
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string SeasonName { get; set; } = "";
    public ListVersionStatus Status { get; set; }
    public string Source { get; set; } = "";
    public string ContentHash { get; set; } = "";
    public int EntryCount { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}
