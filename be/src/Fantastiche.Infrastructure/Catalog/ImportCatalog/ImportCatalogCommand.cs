using Fantastiche.Core.Auth;
using Fantastiche.Infrastructure.Common;

namespace Fantastiche.Infrastructure.Catalog;

public sealed record ImportCatalogCommand(RequestContext Context, string SeasonName, string Csv) : IRequest<ListVersionView>;
