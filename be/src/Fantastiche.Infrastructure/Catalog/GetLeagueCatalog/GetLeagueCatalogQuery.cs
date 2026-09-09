using Fantastiche.Core.Auth;
using Fantastiche.Infrastructure.Common;

namespace Fantastiche.Infrastructure.Catalog;

public sealed record GetLeagueCatalogQuery(RequestContext Context, Guid LeagueId, Guid LeagueSeasonId)
    : IRequest<ListVersionView?>;
