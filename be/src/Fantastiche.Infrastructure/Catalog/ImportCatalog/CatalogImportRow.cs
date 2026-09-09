namespace Fantastiche.Infrastructure.Catalog;

public sealed record CatalogImportRow(
    string ExternalId,
    string Name,
    string FullName,
    string Role,
    string ClubName,
    DateTime BirthDate,
    string Nationality,
    string PreferredFoot);
