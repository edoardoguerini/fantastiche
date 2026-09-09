using Fantastiche.Core.Auth;
using Fantastiche.Infrastructure.Common;
namespace Fantastiche.Infrastructure.Leagues;

public sealed record GetLeagueQuery(RequestContext Context, Guid LeagueId) : IRequest<LeagueDetails>;
