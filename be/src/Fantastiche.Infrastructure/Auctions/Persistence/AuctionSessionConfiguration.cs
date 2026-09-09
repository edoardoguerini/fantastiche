using Fantastiche.Infrastructure.Catalog;
using Fantastiche.Infrastructure.Common.Authentication;
using Fantastiche.Infrastructure.Leagues;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantastiche.Infrastructure.Auctions;

public sealed class AuctionSessionConfiguration : IEntityTypeConfiguration<AuctionSession>
{
    public void Configure(EntityTypeBuilder<AuctionSession> b)
    {
        b.ToTable("AuctionSessions", table =>
        {
            table.HasCheckConstraint("CK_AuctionSessions_Status", "[Status] IN (0, 1, 2)");
            table.HasCheckConstraint("CK_AuctionSessions_CurrentPosition", "[CurrentPosition] >= 0");
            table.HasCheckConstraint("CK_AuctionSessions_Version", "[Version] >= 1");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Status).HasConversion<int>();
        b.HasAlternateKey(x => new { x.Id, x.LeagueSeasonId, x.LeagueId });
        b.HasAlternateKey(x => new { x.Id, x.LeagueSeasonId, x.LeagueId, x.ListVersionId });
        b.HasIndex(x => x.LeagueSeasonId).IsUnique().HasFilter("[Status] < 2");
        b.HasOne<LeagueSeason>().WithMany()
            .HasForeignKey(x => new { x.LeagueSeasonId, x.LeagueId })
            .HasPrincipalKey(x => new { x.Id, x.LeagueId })
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ListVersion>().WithMany().HasForeignKey(x => x.ListVersionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
