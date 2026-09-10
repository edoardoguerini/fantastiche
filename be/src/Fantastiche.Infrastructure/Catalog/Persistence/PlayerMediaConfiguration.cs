using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantastiche.Infrastructure.Catalog;

public sealed class PlayerMediaConfiguration : IEntityTypeConfiguration<PlayerMedia>
{
    public void Configure(EntityTypeBuilder<PlayerMedia> b)
    {
        b.ToTable("PlayerMedia", table => table.HasCheckConstraint("CK_PlayerMedia_ContentLength", "[ContentLength] > 0"));
        b.HasKey(x => new { x.Source, x.ExternalId });
        b.Property(x => x.Source).HasMaxLength(50);
        b.Property(x => x.ExternalId).HasMaxLength(32);
        b.Property(x => x.SourceUrl).HasMaxLength(2048).IsRequired();
        b.Property(x => x.BlobName).HasMaxLength(512).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        b.Property(x => x.Sha256).HasMaxLength(64).IsUnicode(false).IsRequired();
    }
}
