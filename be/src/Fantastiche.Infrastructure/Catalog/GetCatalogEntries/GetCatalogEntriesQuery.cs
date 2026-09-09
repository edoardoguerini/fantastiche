using Fantastiche.Core.Auth;
using Fantastiche.Infrastructure.Common;

namespace Fantastiche.Infrastructure.Catalog;

public sealed record GetCatalogEntriesQuery(
    RequestContext Context,
    Guid ListVersionId,
    string? Search = null,
    string? Role = null,
    string? Club = null,
    int Page = 1,
    int PageSize = 50) : IRequest<CatalogPage<CatalogEntryView>>;
