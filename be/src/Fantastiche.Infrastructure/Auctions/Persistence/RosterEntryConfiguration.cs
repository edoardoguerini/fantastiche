using Fantastiche.Infrastructure.Catalog;
using Fantastiche.Infrastructure.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantastiche.Infrastructure.Auctions;

public sealed class RosterEntryConfiguration : IEntityTypeConfiguration<RosterEntry>
{
    public void Configure(EntityTypeBuilder<RosterEntry> b)
    {
        b.ToTable("RosterEntries", table =>
        {
            table.HasCheckConstraint("CK_RosterEntries_Price", "[Price] > 0");
            table.HasCheckConstraint("CK_RosterEntries_Role", "[Role] IN (N'P', N'D', N'C', N'A')");
        });
        b.HasKey(x => new { x.LeagueSeasonId, x.PlayerId });
        b.Property(x => x.Role).HasMaxLength(1).IsFixedLength().IsRequired();
        b.HasIndex(x => x.PlayerAuctionId).IsUnique();
        b.HasOne<PlayerAuction>().WithMany()
            .HasForeignKey(x => new { x.PlayerAuctionId, x.LeagueSeasonId, x.LeagueId })
            .HasPrincipalKey(x => new { x.Id, x.LeagueSeasonId, x.LeagueId })
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Team>().WithMany()
            .HasForeignKey(x => new { x.TeamId, x.LeagueSeasonId, x.LeagueId })
            .HasPrincipalKey(x => new { x.Id, x.LeagueSeasonId, x.LeagueId })
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Player>().WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Restrict);
    }
}
