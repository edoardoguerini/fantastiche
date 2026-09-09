namespace Fantastiche.Infrastructure.Auctions;

public sealed class RosterEntry
{
    public Guid LeagueSeasonId { get; set; }
    public Guid PlayerId { get; set; }
    public Guid LeagueId { get; set; }
    public Guid TeamId { get; set; }
    public Guid PlayerAuctionId { get; set; }
    public string Role { get; set; } = "";
    public int Price { get; set; }
    public DateTimeOffset AcquiredAt { get; set; }
}
