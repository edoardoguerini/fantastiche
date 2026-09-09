using Fantastiche.Infrastructure.Common.Authentication;
using Fantastiche.Infrastructure.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantastiche.Infrastructure.Auctions;

public sealed class BidConfiguration : IEntityTypeConfiguration<Bid>
{
    public void Configure(EntityTypeBuilder<Bid> b)
    {
        b.ToTable("Bids", table => table.HasCheckConstraint("CK_Bids_Amount", "[Amount] > 0"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.HasIndex(x => new { x.PlayerAuctionId, x.Sequence }).IsUnique();
        b.HasOne<PlayerAuction>().WithMany()
            .HasForeignKey(x => new { x.PlayerAuctionId, x.LeagueSeasonId, x.LeagueId })
            .HasPrincipalKey(x => new { x.Id, x.LeagueSeasonId, x.LeagueId })
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Team>().WithMany()
            .HasForeignKey(x => new { x.TeamId, x.LeagueSeasonId, x.LeagueId })
            .HasPrincipalKey(x => new { x.Id, x.LeagueSeasonId, x.LeagueId })
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
