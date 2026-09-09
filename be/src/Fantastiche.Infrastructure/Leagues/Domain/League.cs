namespace Fantastiche.Infrastructure.Leagues;

public sealed class League
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Name { get; set; } = "";
    public string? LogoBlobName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
