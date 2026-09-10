namespace Fantastiche.Infrastructure.Leagues;

public sealed record LeagueDetails(Guid Id, string Name, Guid LeagueSeasonId, string SeasonName, int Budget, int Goalkeepers, int Defenders, int Midfielders, int Forwards);
public sealed record LeaguePage<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
public sealed record InvitationDetails(Guid Id, Guid LeagueId, DateTimeOffset ExpiresAt);
public sealed record InvitationPreview(string LeagueName, DateTimeOffset ExpiresAt, bool RequiresLogin, bool RequiresTeam);
public sealed record AcceptanceDetails(Guid LeagueId, Guid LeagueSeasonId, Guid? TeamId);
