using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Fantastiche.Infrastructure.Common.Authentication;
using Fantastiche.Infrastructure.Leagues;
using Fantastiche.Infrastructure.Teams;
using Fantastiche.Infrastructure.Emails;
namespace Fantastiche.Infrastructure.Leagues;

public sealed class LeagueInvitationConfiguration : IEntityTypeConfiguration<LeagueInvitation>
{
    public void Configure(EntityTypeBuilder<LeagueInvitation> b)
    {
        b.ToTable("LeagueInvitations"); b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Email).HasMaxLength(256).IsRequired(); b.Property(x => x.TokenHash).HasMaxLength(64).IsFixedLength().IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique(); b.HasIndex(x => new { x.LeagueSeasonId, x.UserId, x.Kind }); b.Property(x => x.Kind).HasConversion<int>();
        b.HasOne<LeagueSeason>().WithMany().HasForeignKey(x => new { x.LeagueSeasonId, x.LeagueId }).HasPrincipalKey(x => new { x.Id, x.LeagueId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<LeagueMember>().WithMany().HasForeignKey(x => new { x.LeagueId, x.UserId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.InvitedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Team>().WithMany().HasForeignKey(x => new { x.AcceptedTeamId, x.LeagueSeasonId, x.LeagueId }).HasPrincipalKey(x => new { x.Id, x.LeagueSeasonId, x.LeagueId }).OnDelete(DeleteBehavior.Restrict);
    }
}
