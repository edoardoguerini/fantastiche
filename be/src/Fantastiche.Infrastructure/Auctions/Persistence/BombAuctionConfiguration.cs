using Fantastiche.Infrastructure.Catalog;
using Fantastiche.Infrastructure.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantastiche.Infrastructure.Auctions;

public sealed class BombAuctionConfiguration : IEntityTypeConfiguration<BombAuction>
{
    public void Configure(EntityTypeBuilder<BombAuction> b)
    {
        b.ToTable("BombAuctions", t =>
        {
            t.HasCheckConstraint("CK_BombAuctions_Status", "[Status] IN (-1, 0, 1, 2, 3, 4)");
            t.HasCheckConstraint("CK_BombAuctions_Round", "[Round] > 0 AND [MinimumAmount] > 0 AND [RevealedCount] >= 0");
            t.HasCheckConstraint("CK_BombAuctions_Role", "[Role] IN (N'P', N'D', N'C', N'A')");
            t.HasCheckConstraint("CK_BombAuctions_Winner", "([Status] = 2 AND [PlayerAuctionId] IS NOT NULL AND [WinningTeamId] IS NOT NULL AND [WinningAmount] IS NOT NULL AND [WinningAmount] > 0) OR ([Status] <> 2 AND [PlayerAuctionId] IS NULL AND [WinningTeamId] IS NULL AND [WinningAmount] IS NULL)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Role).HasMaxLength(1).IsFixedLength().IsRequired();
        b.Property(x => x.Status).HasConversion<int>();
        b.HasAlternateKey(x => new { x.Id, x.LeagueSeasonId, x.LeagueId });
        b.HasIndex(x => x.SessionId).IsUnique().HasFilter("[Status] < 2");
        b.HasIndex(x => new { x.Deadline, x.Id }).HasFilter("[Status] < 1");
        b.HasIndex(x => new { x.NextRevealAt, x.Id }).HasFilter("[Status] = 1");
        b.HasOne<AuctionSession>().WithMany().HasForeignKey(x => new { x.SessionId, x.LeagueSeasonId, x.LeagueId, x.ListVersionId })
            .HasPrincipalKey(x => new { x.Id, x.LeagueSeasonId, x.LeagueId, x.ListVersionId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ListEntry>().WithMany().HasForeignKey(x => new { x.ListVersionId, x.PlayerId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Team>().WithMany().HasForeignKey(x => new { x.CallerTeamId, x.LeagueSeasonId, x.LeagueId })
            .HasPrincipalKey(x => new { x.Id, x.LeagueSeasonId, x.LeagueId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Team>().WithMany().HasForeignKey(x => new { x.WinningTeamId, x.LeagueSeasonId, x.LeagueId })
            .HasPrincipalKey(x => new { x.Id, x.LeagueSeasonId, x.LeagueId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PlayerAuction>().WithMany().HasForeignKey(x => new { x.PlayerAuctionId, x.LeagueSeasonId, x.LeagueId })
            .HasPrincipalKey(x => new { x.Id, x.LeagueSeasonId, x.LeagueId }).OnDelete(DeleteBehavior.Restrict);
    }
}
