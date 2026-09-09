namespace Fantastiche.Infrastructure.Leagues;

public enum InvitationKind { Organizer, Participant }
public sealed class LeagueInvitation
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid LeagueId { get; set; }
    public Guid LeagueSeasonId { get; set; }
    public Guid UserId { get; set; }
    public Guid InvitedByUserId { get; set; }
    public string Email { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public InvitationKind Kind { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? AcceptedTeamId { get; set; }
}
