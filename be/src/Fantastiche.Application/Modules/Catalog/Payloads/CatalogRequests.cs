using Fantastiche.Infrastructure.Catalog;
using FluentValidation;

namespace Fantastiche.Application.Modules.Catalog;

public sealed record ImportCatalogRequest(string SeasonName, string Csv);
public sealed class ImportCatalogRequestValidator : AbstractValidator<ImportCatalogRequest>
{
    public ImportCatalogRequestValidator()
    {
        RuleFor(x => x.SeasonName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Csv).NotEmpty().MaximumLength(FantacalcioCsvParser.MaxCsvLength);
    }
}

public sealed record CatalogVersionsRequest(string? SeasonName, int Page = 1, int PageSize = 50);
public sealed class CatalogVersionsRequestValidator : AbstractValidator<CatalogVersionsRequest>
{
    public CatalogVersionsRequestValidator()
    {
        RuleFor(x => x.SeasonName).MaximumLength(50);
        RuleFor(x => x.Page).InclusiveBetween(1, 10000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed record CatalogEntriesRequest(string? Search, string? Role, string? Club, int Page = 1, int PageSize = 50);
public sealed class CatalogEntriesRequestValidator : AbstractValidator<CatalogEntriesRequest>
{
    public CatalogEntriesRequestValidator()
    {
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.Club).MaximumLength(100);
        RuleFor(x => x.Role).Must(role => string.IsNullOrWhiteSpace(role) || role.Trim() is "P" or "D" or "C" or "A")
            .WithMessage("Il ruolo Classic deve essere P, D, C o A.");
        RuleFor(x => x.Page).InclusiveBetween(1, 10000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
