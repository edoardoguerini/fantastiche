using Fantastiche.Infrastructure.Common.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantastiche.Infrastructure.Catalog;

public sealed class ListVersionConfiguration : IEntityTypeConfiguration<ListVersion>
{
    public void Configure(EntityTypeBuilder<ListVersion> b)
    {
        b.ToTable("ListVersions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.SeasonName).HasMaxLength(50).IsRequired();
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.Source).HasMaxLength(50).IsRequired();
        b.Property(x => x.ContentHash).HasMaxLength(64).IsFixedLength().IsRequired();
        b.HasIndex(x => new { x.Source, x.SeasonName, x.ContentHash }).IsUnique();
        b.HasIndex(x => new { x.Status, x.SeasonName, x.CreatedAt });
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
