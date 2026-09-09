using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Fantastiche.Infrastructure.Common.Authentication;
using Fantastiche.Infrastructure.Leagues;
using Fantastiche.Infrastructure.Teams;
using Fantastiche.Infrastructure.Emails;
namespace Fantastiche.Infrastructure.Leagues;

public sealed class LeagueMemberConfiguration : IEntityTypeConfiguration<LeagueMember>
{
    public void Configure(EntityTypeBuilder<LeagueMember> b)
    {
        b.ToTable("LeagueMembers"); b.HasKey(x => new { x.LeagueId, x.UserId });
        b.Property(x => x.Status).HasConversion<int>();
        b.HasOne<League>().WithMany().HasForeignKey(x => x.LeagueId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
