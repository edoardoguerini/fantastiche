using Fantastiche.Core.Auth;
using Fantastiche.Infrastructure.Common;

namespace Fantastiche.Infrastructure.Catalog;

public sealed record SetLeagueCatalogCommand(RequestContext Context, Guid LeagueId, Guid LeagueSeasonId, Guid ListVersionId)
    : IRequest<LeagueCatalogView>;
