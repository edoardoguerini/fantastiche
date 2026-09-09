namespace Fantastiche.Infrastructure.Auctions;

public sealed class BudgetMovement
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid LeagueSeasonId { get; set; }
    public Guid LeagueId { get; set; }
    public Guid TeamId { get; set; }
    public Guid PlayerAuctionId { get; set; }
    public int Amount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
