using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Fantastiche.Infrastructure.Common.Authentication;
using Fantastiche.Infrastructure.Leagues;
using Fantastiche.Infrastructure.Teams;
using Fantastiche.Infrastructure.Emails;
namespace Fantastiche.Infrastructure.Teams;

public sealed class TeamMemberConfiguration : IEntityTypeConfiguration<TeamMember>
{
    public void Configure(EntityTypeBuilder<TeamMember> b)
    {
        b.ToTable("TeamMembers"); b.HasKey(x => new { x.TeamId, x.UserId });
        b.HasIndex(x => new { x.LeagueSeasonId, x.UserId }).IsUnique();
        b.HasOne<Team>().WithMany().HasForeignKey(x => new { x.TeamId, x.LeagueSeasonId, x.LeagueId }).HasPrincipalKey(x => new { x.Id, x.LeagueSeasonId, x.LeagueId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<LeagueMember>().WithMany().HasForeignKey(x => new { x.LeagueId, x.UserId }).OnDelete(DeleteBehavior.Restrict);
    }
}
