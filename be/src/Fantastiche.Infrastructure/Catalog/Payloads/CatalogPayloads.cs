namespace Fantastiche.Infrastructure.Catalog;

public sealed record CatalogPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

public sealed record ListVersionView(
    Guid Id,
    string SeasonName,
    string Status,
    string Source,
    string ContentHash,
    int EntryCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt);

public sealed record CatalogEntryView(
    Guid PlayerId,
    string ExternalId,
    string Name,
    string FullName,
    string Role,
    string ClubName,
    DateTime BirthDate,
    string Nationality,
    string PreferredFoot,
    string? PhotoUrl = null,
    string? ClubLogoUrl = null,
    string? MantraRole = null,
    int? CurrentQuotation = null,
    int? InitialQuotation = null,
    int? CurrentMantraQuotation = null,
    int? InitialMantraQuotation = null,
    int? Fvm = null,
    int? MantraFvm = null,
    bool? IsTransferred = null);

public sealed record LeagueCatalogView(Guid LeagueId, Guid LeagueSeasonId, Guid ListVersionId);
