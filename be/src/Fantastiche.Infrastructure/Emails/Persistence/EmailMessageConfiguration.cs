using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Fantastiche.Infrastructure.Common.Authentication;
using Fantastiche.Infrastructure.Leagues;
using Fantastiche.Infrastructure.Teams;
using Fantastiche.Infrastructure.Emails;
namespace Fantastiche.Infrastructure.Emails;

public sealed class EmailMessageConfiguration : IEntityTypeConfiguration<EmailMessage>
{
    public void Configure(EntityTypeBuilder<EmailMessage> b)
    {
        b.ToTable("EmailMessages"); b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.ToAddress).HasMaxLength(256).IsRequired(); b.Property(x => x.ProtectedPayload).IsRequired();
        b.Property(x => x.Status).HasConversion<int>(); b.Property(x => x.ProviderMessageId).HasMaxLength(500); b.Property(x => x.LastErrorCode).HasMaxLength(100);
        b.HasIndex(x => new { x.Status, x.NextAttemptAt, x.LeaseExpiresAt });
        b.HasOne<LeagueInvitation>().WithMany().HasForeignKey(x => x.InvitationId).OnDelete(DeleteBehavior.Restrict);
    }
}
