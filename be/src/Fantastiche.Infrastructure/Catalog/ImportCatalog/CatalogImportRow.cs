namespace Fantastiche.Infrastructure.Catalog;

public sealed record CatalogImportRow(
    string ExternalId,
    string Name,
    string FullName,
    string Role,
    string ClubName,
    DateTime BirthDate,
    string Nationality,
    string PreferredFoot,
    string MantraRole,
    int CurrentQuotation,
    int InitialQuotation,
    int CurrentMantraQuotation,
    int InitialMantraQuotation,
    int Fvm,
    int MantraFvm,
    bool IsTransferred);
