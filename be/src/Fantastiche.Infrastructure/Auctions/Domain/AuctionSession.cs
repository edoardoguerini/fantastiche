namespace Fantastiche.Infrastructure.Auctions;

public enum AuctionSessionStatus
{
    Active,
    Paused,
    Completed
}

public sealed class AuctionSession
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid LeagueId { get; set; }
    public Guid LeagueSeasonId { get; set; }
    public Guid ListVersionId { get; set; }
    public AuctionSessionStatus Status { get; set; }
    public int CurrentPosition { get; set; }
    public long Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
}
