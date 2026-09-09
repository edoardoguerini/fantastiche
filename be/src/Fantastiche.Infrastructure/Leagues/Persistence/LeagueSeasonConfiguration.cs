using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Fantastiche.Infrastructure.Common.Authentication;
using Fantastiche.Infrastructure.Leagues;
using Fantastiche.Infrastructure.Teams;
using Fantastiche.Infrastructure.Emails;
using Fantastiche.Infrastructure.Catalog;
namespace Fantastiche.Infrastructure.Leagues;

public sealed class LeagueSeasonConfiguration : IEntityTypeConfiguration<LeagueSeason>
{
    public void Configure(EntityTypeBuilder<LeagueSeason> b)
    {
        b.ToTable("LeagueSeasons", t => t.HasCheckConstraint("CK_LeagueSeasons_Rules", "[Budget] > 0 AND [Goalkeepers] >= 0 AND [Defenders] >= 0 AND [Midfielders] >= 0 AND [Forwards] >= 0 AND ([Goalkeepers]+[Defenders]+[Midfielders]+[Forwards]) > 0 AND [Budget] >= ([Goalkeepers]+[Defenders]+[Midfielders]+[Forwards])"));
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever(); b.HasAlternateKey(x => new { x.Id, x.LeagueId });
        b.Property(x => x.Name).HasMaxLength(50).IsRequired(); b.HasIndex(x => new { x.LeagueId, x.Name }).IsUnique();
        b.HasOne<League>().WithMany().HasForeignKey(x => x.LeagueId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ListVersion>().WithMany().HasForeignKey(x => x.ListVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}
