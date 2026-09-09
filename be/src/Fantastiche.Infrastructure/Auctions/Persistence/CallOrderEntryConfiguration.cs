using Fantastiche.Infrastructure.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantastiche.Infrastructure.Auctions;

public sealed class CallOrderEntryConfiguration : IEntityTypeConfiguration<CallOrderEntry>
{
    public void Configure(EntityTypeBuilder<CallOrderEntry> b)
    {
        b.ToTable("CallOrderEntries");
        b.HasKey(x => new { x.SessionId, x.TeamId });
        b.HasIndex(x => new { x.SessionId, x.Position }).IsUnique();
        b.HasOne<AuctionSession>().WithMany()
            .HasForeignKey(x => new { x.SessionId, x.LeagueSeasonId, x.LeagueId })
            .HasPrincipalKey(x => new { x.Id, x.LeagueSeasonId, x.LeagueId })
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Team>().WithMany()
            .HasForeignKey(x => new { x.TeamId, x.LeagueSeasonId, x.LeagueId })
            .HasPrincipalKey(x => new { x.Id, x.LeagueSeasonId, x.LeagueId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
