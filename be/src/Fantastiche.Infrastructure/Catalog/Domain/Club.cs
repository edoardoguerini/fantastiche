namespace Fantastiche.Infrastructure.Catalog;

public sealed class Club
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Source { get; set; } = "";
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
}
