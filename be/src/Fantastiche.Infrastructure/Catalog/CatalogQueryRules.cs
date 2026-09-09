using Fantastiche.Core.Exceptions;

namespace Fantastiche.Infrastructure.Catalog;

internal static class CatalogQueryRules
{
    public static void RequireAuthenticated(Guid? userId)
    {
        if (userId is null) throw new DomainException("auth.required", "Accesso richiesto.", 401);
    }

    public static void ValidatePage(int page, int pageSize)
    {
        if (page is < 1 or > 10_000 || pageSize is < 1 or > 100)
            throw InvalidQuery();
    }

    public static string? Optional(string? value, int maxLength)
    {
        if (value is null) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength) throw InvalidQuery();
        return trimmed.Length == 0 ? null : trimmed;
    }

    public static string EscapeLike(string value) => value
        .Replace("~", "~~", StringComparison.Ordinal)
        .Replace("%", "~%", StringComparison.Ordinal)
        .Replace("_", "~_", StringComparison.Ordinal)
        .Replace("[", "~[", StringComparison.Ordinal);

    public static DomainException InvalidQuery() =>
        new("catalog.invalid_query", "Filtri o paginazione del catalogo non validi.");

    public static DomainException NotFound() =>
        new("resource.not_found", "Risorsa non disponibile.", 404);

    public static DomainException Forbidden() =>
        new("auth.forbidden", "Operazione non consentita.", 403);
}
