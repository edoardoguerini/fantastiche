namespace Fantastiche.Infrastructure.Catalog;

public sealed class Player
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Source { get; set; } = "";
    public string ExternalId { get; set; } = "";
}
