using Fantastiche.Infrastructure.Common.Authentication;
using Fantastiche.Infrastructure.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantastiche.Infrastructure.Auctions;

public sealed class BombOfferConfiguration : IEntityTypeConfiguration<BombOffer>
{
    public void Configure(EntityTypeBuilder<BombOffer> b)
    {
        b.ToTable("BombOffers", t =>
        {
            t.HasCheckConstraint("CK_BombOffers_Round", "[Round] > 0 AND [Position] >= 0");
            t.HasCheckConstraint("CK_BombOffers_Submission", "([Amount] IS NULL AND [UserId] IS NULL AND [SubmittedAt] IS NULL) OR ([Amount] IS NOT NULL AND [Amount] > 0 AND [UserId] IS NOT NULL AND [SubmittedAt] IS NOT NULL)");
        });
        b.HasKey(x => new { x.BombAuctionId, x.Round, x.TeamId });
        b.HasIndex(x => new { x.BombAuctionId, x.Round, x.Position }).IsUnique();
        b.HasOne<BombAuction>().WithMany().HasForeignKey(x => new { x.BombAuctionId, x.LeagueSeasonId, x.LeagueId })
            .HasPrincipalKey(x => new { x.Id, x.LeagueSeasonId, x.LeagueId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Team>().WithMany().HasForeignKey(x => new { x.TeamId, x.LeagueSeasonId, x.LeagueId })
            .HasPrincipalKey(x => new { x.Id, x.LeagueSeasonId, x.LeagueId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
