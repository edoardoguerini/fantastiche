namespace Fantastiche.Infrastructure.Auctions;

public sealed class CommandReceipt
{
    public Guid SessionId { get; set; }
    public Guid UserId { get; set; }
    public Guid RequestId { get; set; }
    public string CommandType { get; set; } = "";
    public string PayloadHash { get; set; } = "";
    public string ResultJson { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
