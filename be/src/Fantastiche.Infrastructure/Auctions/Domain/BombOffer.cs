namespace Fantastiche.Infrastructure.Auctions;

// Una riga per partecipante e turno; gli importi assenti non sono offerte confermate.
public sealed class BombOffer
{
    public Guid BombAuctionId { get; set; }
    public int Round { get; set; }
    public Guid TeamId { get; set; }
    public Guid LeagueSeasonId { get; set; }
    public Guid LeagueId { get; set; }
    public int Position { get; set; }
    public int? Amount { get; set; }
    public Guid? UserId { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
}
