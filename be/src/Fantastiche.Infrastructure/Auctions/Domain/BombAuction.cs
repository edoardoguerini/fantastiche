namespace Fantastiche.Infrastructure.Auctions;

public enum BombAuctionStatus { Waiting = -1, Collecting = 0, Revealing = 1, Completed = 2, Cancelled = 3, NoSale = 4 }

public sealed class BombAuction
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid SessionId { get; set; }
    public Guid LeagueSeasonId { get; set; }
    public Guid LeagueId { get; set; }
    public Guid ListVersionId { get; set; }
    public Guid PlayerId { get; set; }
    public Guid CallerTeamId { get; set; }
    public string Role { get; set; } = "";
    public BombAuctionStatus Status { get; set; }
    public int Round { get; set; } = 1;
    public int MinimumAmount { get; set; } = 1;
    public DateTimeOffset Deadline { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? RevealStartedAt { get; set; }
    public DateTimeOffset? NextRevealAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public int RevealedCount { get; set; }
    public Guid? PlayerAuctionId { get; set; }
    public Guid? WinningTeamId { get; set; }
    public int? WinningAmount { get; set; }
}
