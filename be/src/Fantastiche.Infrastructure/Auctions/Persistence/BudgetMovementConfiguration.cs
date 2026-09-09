using Fantastiche.Infrastructure.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantastiche.Infrastructure.Auctions;

public sealed class BudgetMovementConfiguration : IEntityTypeConfiguration<BudgetMovement>
{
    public void Configure(EntityTypeBuilder<BudgetMovement> b)
    {
        b.ToTable("BudgetMovements", table => table.HasCheckConstraint("CK_BudgetMovements_Amount", "[Amount] < 0"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.HasIndex(x => x.PlayerAuctionId).IsUnique();
        b.HasOne<PlayerAuction>().WithMany()
            .HasForeignKey(x => new { x.PlayerAuctionId, x.LeagueSeasonId, x.LeagueId })
            .HasPrincipalKey(x => new { x.Id, x.LeagueSeasonId, x.LeagueId })
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Team>().WithMany()
            .HasForeignKey(x => new { x.TeamId, x.LeagueSeasonId, x.LeagueId })
            .HasPrincipalKey(x => new { x.Id, x.LeagueSeasonId, x.LeagueId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
