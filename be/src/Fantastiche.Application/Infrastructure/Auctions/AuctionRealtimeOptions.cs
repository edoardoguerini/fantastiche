namespace Fantastiche.Application.Infrastructure.Auctions;

public sealed class AuctionRealtimeOptions
{
    public const string CorsPolicyName = "AuctionRealtimeOrigins";

    public string[] AllowedOrigins { get; set; } = [];

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(500);
}
