namespace Fantastiche.Infrastructure.Catalog;

public sealed class ListEntry
{
    public Guid ListVersionId { get; set; }
    public Guid PlayerId { get; set; }
    public Guid ClubId { get; set; }
    public string Name { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Role { get; set; } = "";
    public string ClubName { get; set; } = "";
    public DateTime BirthDate { get; set; }
    public string Nationality { get; set; } = "";
    public string PreferredFoot { get; set; } = "";
}
