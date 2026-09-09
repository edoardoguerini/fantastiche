namespace Fantastiche.Infrastructure.Teams;

public sealed class Team
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid LeagueId { get; set; }
    public Guid LeagueSeasonId { get; set; }
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public int Budget { get; set; }
}
