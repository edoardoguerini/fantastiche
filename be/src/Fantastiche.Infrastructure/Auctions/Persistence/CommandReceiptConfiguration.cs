using Fantastiche.Infrastructure.Common.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantastiche.Infrastructure.Auctions;

public sealed class CommandReceiptConfiguration : IEntityTypeConfiguration<CommandReceipt>
{
    public void Configure(EntityTypeBuilder<CommandReceipt> b)
    {
        b.ToTable("CommandReceipts");
        b.HasKey(x => new { x.SessionId, x.UserId, x.RequestId });
        b.Property(x => x.CommandType).HasMaxLength(32).IsRequired();
        b.Property(x => x.PayloadHash).HasMaxLength(64).IsFixedLength().IsRequired();
        b.Property(x => x.ResultJson).HasMaxLength(4000).IsRequired();
        b.HasOne<AuctionSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
