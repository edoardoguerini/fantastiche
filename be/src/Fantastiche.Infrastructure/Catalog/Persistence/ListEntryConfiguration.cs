using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantastiche.Infrastructure.Catalog;

public sealed class ListEntryConfiguration : IEntityTypeConfiguration<ListEntry>
{
    public void Configure(EntityTypeBuilder<ListEntry> b)
    {
        b.ToTable("ListEntries");
        b.HasKey(x => new { x.ListVersionId, x.PlayerId });
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Role).HasMaxLength(1).IsFixedLength().IsRequired();
        b.Property(x => x.ClubName).HasMaxLength(100).IsRequired();
        b.Property(x => x.BirthDate).HasColumnType("date");
        b.Property(x => x.Nationality).HasMaxLength(200).IsRequired();
        b.Property(x => x.PreferredFoot).HasMaxLength(50).IsRequired();
        b.Property(x => x.MantraRole).HasMaxLength(50);
        b.HasIndex(x => new { x.ListVersionId, x.Role, x.ClubName });
        b.HasOne<ListVersion>().WithMany().HasForeignKey(x => x.ListVersionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Player>().WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Club>().WithMany().HasForeignKey(x => x.ClubId).OnDelete(DeleteBehavior.Restrict);
    }
}
