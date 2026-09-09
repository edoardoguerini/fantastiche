namespace Fantastiche.Infrastructure.Teams;

public sealed class TeamMember
{
    public Guid TeamId { get; set; }
    public Guid LeagueId { get; set; }
    public Guid LeagueSeasonId { get; set; }
    public Guid UserId { get; set; }
}
