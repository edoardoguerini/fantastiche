namespace Fantastiche.Infrastructure.Auctions;

public enum PlayerAuctionStatus
{
    Open,
    Closed
}

public sealed class PlayerAuction
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid SessionId { get; set; }
    public Guid LeagueSeasonId { get; set; }
    public Guid LeagueId { get; set; }
    public Guid ListVersionId { get; set; }
    public Guid PlayerId { get; set; }
    public int Number { get; set; }
    public Guid CallerTeamId { get; set; }
    public Guid WinningTeamId { get; set; }
    public string Role { get; set; } = "";
    public int DurationSeconds { get; set; }
    public string IncrementOptionsJson { get; set; } = "";
    public int CurrentAmount { get; set; }
    public int BidSequence { get; set; }
    public DateTimeOffset Deadline { get; set; }
    public PlayerAuctionStatus Status { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
}
