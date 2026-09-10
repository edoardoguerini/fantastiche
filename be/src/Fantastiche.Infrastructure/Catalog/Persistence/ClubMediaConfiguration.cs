using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantastiche.Infrastructure.Catalog;

public sealed class ClubMediaConfiguration : IEntityTypeConfiguration<ClubMedia>
{
    public void Configure(EntityTypeBuilder<ClubMedia> b)
    {
        b.ToTable("ClubMedia", table => table.HasCheckConstraint("CK_ClubMedia_ContentLength", "[ContentLength] > 0"));
        b.HasKey(x => new { x.Source, x.NormalizedClubName });
        b.Property(x => x.Source).HasMaxLength(50);
        b.Property(x => x.NormalizedClubName).HasMaxLength(100);
        b.Property(x => x.SourceUrl).HasMaxLength(2048).IsRequired();
        b.Property(x => x.BlobName).HasMaxLength(512).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        b.Property(x => x.Sha256).HasMaxLength(64).IsUnicode(false).IsRequired();
    }
}
