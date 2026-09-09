namespace Fantastiche.Infrastructure.Emails;

public enum EmailStatus { Pending, Processing, AcceptedByProvider, Failed, Cancelled }
public sealed class EmailMessage
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid InvitationId { get; set; }
    public string ToAddress { get; set; } = "";
    public string ProtectedPayload { get; set; } = "";
    public EmailStatus Status { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? LastErrorCode { get; set; }
}
