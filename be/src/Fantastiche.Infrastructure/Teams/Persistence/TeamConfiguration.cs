using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Fantastiche.Infrastructure.Common.Authentication;
using Fantastiche.Infrastructure.Leagues;
using Fantastiche.Infrastructure.Teams;
using Fantastiche.Infrastructure.Emails;
namespace Fantastiche.Infrastructure.Teams;

public sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> b)
    {
        b.ToTable("Teams", t => t.HasCheckConstraint("CK_Teams_Budget", "[Budget] >= 0")); b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.HasAlternateKey(x => new { x.Id, x.LeagueSeasonId, x.LeagueId });
        b.Property(x => x.Name).HasMaxLength(100).IsRequired(); b.Property(x => x.NormalizedName).HasMaxLength(100).IsRequired();
        b.HasIndex(x => new { x.LeagueSeasonId, x.NormalizedName }).IsUnique();
        b.HasOne<LeagueSeason>().WithMany().HasForeignKey(x => new { x.LeagueSeasonId, x.LeagueId }).HasPrincipalKey(x => new { x.Id, x.LeagueId }).OnDelete(DeleteBehavior.Restrict);
    }
}
