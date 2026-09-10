using Fantastiche.Core.Auth;
using Fantastiche.Infrastructure.Common;

namespace Fantastiche.Infrastructure.Leagues;

public sealed record GetLeaguesQuery(RequestContext Context, int Page = 1, int PageSize = 20)
    : IRequest<LeaguePage<LeagueDetails>>;
