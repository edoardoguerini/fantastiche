using Fantastiche.Core.Auth;
using Fantastiche.Infrastructure.Common;

namespace Fantastiche.Infrastructure.Leagues;

public sealed record GetLeagueParticipantsQuery(RequestContext Context, Guid LeagueId, Guid LeagueSeasonId, int Page = 1, int PageSize = 20)
    : IRequest<LeagueParticipantsView>;

public sealed record LeagueParticipantView(Guid UserId, string DisplayName, string? TeamName, bool IsOrganizer);
public sealed record LeagueInvitationView(Guid Id, string DisplayName, string Email, string Kind, string Status, DateTimeOffset ExpiresAt);
public sealed record LeagueParticipantsView(Guid LeagueId, Guid LeagueSeasonId, bool CanManage,
    IReadOnlyList<LeagueParticipantView> Participants, LeaguePage<LeagueInvitationView> Invitations);
