using Fantastiche.Core.Auth;
using Fantastiche.Infrastructure.Common;
namespace Fantastiche.Infrastructure.Leagues;

public sealed record RevokeInvitationCommand(RequestContext Context, Guid LeagueId, Guid InvitationId) : IRequest<bool>;
