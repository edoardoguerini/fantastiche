using Fantastiche.Infrastructure.Catalog;
using Fantastiche.Infrastructure.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantastiche.Infrastructure.Auctions;

public sealed class PlayerAuctionConfiguration : IEntityTypeConfiguration<PlayerAuction>
{
    public void Configure(EntityTypeBuilder<PlayerAuction> b)
    {
        b.ToTable("PlayerAuctions", table =>
        {
            table.HasCheckConstraint("CK_PlayerAuctions_DurationSeconds", "[DurationSeconds] IN (5, 10, 15, 20, 25, 30)");
            table.HasCheckConstraint("CK_PlayerAuctions_CurrentAmount", "[CurrentAmount] > 0");
            table.HasCheckConstraint("CK_PlayerAuctions_Status", "[Status] IN (0, 1)");
            table.HasCheckConstraint("CK_PlayerAuctions_Role", "[Role] IN (N'P', N'D', N'C', N'A')");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Role).HasMaxLength(1).IsFixedLength().IsRequired();
        b.Property(x => x.IncrementOptionsJson).HasMaxLength(200).IsRequired();
        b.Property(x => x.Status).HasConversion<int>();
        b.HasAlternateKey(x => new { x.Id, x.LeagueSeasonId, x.LeagueId });
        b.HasIndex(x => new { x.SessionId, x.Number }).IsUnique();
        b.HasIndex(x => x.SessionId).IsUnique().HasFilter("[Status] = 0");
        b.HasIndex(x => new { x.Deadline, x.Id }).HasFilter("[Status] = 0");
        b.HasOne<AuctionSession>().WithMany()
            .HasForeignKey(x => new { x.SessionId, x.LeagueSeasonId, x.LeagueId, x.ListVersionId })
            .HasPrincipalKey(x => new { x.Id, x.LeagueSeasonId, x.LeagueId, x.ListVersionId })
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ListEntry>().WithMany()
            .HasForeignKey(x => new { x.ListVersionId, x.PlayerId })
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Team>().WithMany()
            .HasForeignKey(x => new { x.CallerTeamId, x.LeagueSeasonId, x.LeagueId })
            .HasPrincipalKey(x => new { x.Id, x.LeagueSeasonId, x.LeagueId })
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Team>().WithMany()
            .HasForeignKey(x => new { x.WinningTeamId, x.LeagueSeasonId, x.LeagueId })
            .HasPrincipalKey(x => new { x.Id, x.LeagueSeasonId, x.LeagueId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
