using Fantastiche.Core.Auth;
using Fantastiche.Infrastructure.Common;

namespace Fantastiche.Infrastructure.Catalog;

public sealed record GetCatalogVersionsQuery(RequestContext Context, string? SeasonName = null, int Page = 1, int PageSize = 50)
    : IRequest<CatalogPage<ListVersionView>>;
