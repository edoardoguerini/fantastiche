namespace Fantastiche.Infrastructure.Leagues;

public sealed class LeagueSeason
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid LeagueId { get; set; }
    public string Name { get; set; } = "";
    public Guid? ListVersionId { get; set; }
    public int Budget { get; set; } = 500;
    public int Goalkeepers { get; set; } = 3;
    public int Defenders { get; set; } = 8;
    public int Midfielders { get; set; } = 8;
    public int Forwards { get; set; } = 6;
}
