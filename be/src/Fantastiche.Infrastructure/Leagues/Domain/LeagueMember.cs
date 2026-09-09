namespace Fantastiche.Infrastructure.Leagues;

public enum MembershipStatus { Pending, Active }
public sealed class LeagueMember
{
    public Guid LeagueId { get; set; }
    public Guid UserId { get; set; }
    public MembershipStatus Status { get; set; }
    public bool IsOrganizer { get; set; }
}
