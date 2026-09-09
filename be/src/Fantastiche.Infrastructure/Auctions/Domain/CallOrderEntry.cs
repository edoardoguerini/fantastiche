namespace Fantastiche.Infrastructure.Auctions;

public sealed class CallOrderEntry
{
    public Guid SessionId { get; set; }
    public Guid TeamId { get; set; }
    public Guid LeagueSeasonId { get; set; }
    public Guid LeagueId { get; set; }
    public int Position { get; set; }
}
