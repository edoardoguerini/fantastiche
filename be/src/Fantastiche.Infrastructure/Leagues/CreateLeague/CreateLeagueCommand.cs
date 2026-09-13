using Fantastiche.Core.Auth;
using Fantastiche.Infrastructure.Common;
namespace Fantastiche.Infrastructure.Leagues;

public sealed record CreateLeagueCommand(RequestContext Context, string Name, string SeasonName, string OrganizerEmail, string OrganizerName, int Budget = 500, int Goalkeepers = 3, int Defenders = 8, int Midfielders = 8, int Forwards = 6, byte[]? Logo = null) : IRequest<LeagueDetails>;
