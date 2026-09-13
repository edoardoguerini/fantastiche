using Fantastiche.Core.Auth;
using Fantastiche.Infrastructure.Common;
namespace Fantastiche.Infrastructure.Leagues;

public sealed record InviteMemberCommand(RequestContext Context, Guid LeagueId, Guid LeagueSeasonId, string Email) : IRequest<InvitationDetails>;
