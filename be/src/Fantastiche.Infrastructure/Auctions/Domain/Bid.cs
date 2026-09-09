namespace Fantastiche.Infrastructure.Auctions;

public sealed class Bid
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid PlayerAuctionId { get; set; }
    public Guid LeagueSeasonId { get; set; }
    public Guid LeagueId { get; set; }
    public Guid TeamId { get; set; }
    public Guid UserId { get; set; }
    public int Amount { get; set; }
    public int Sequence { get; set; }
    public DateTimeOffset AcceptedAt { get; set; }
}
