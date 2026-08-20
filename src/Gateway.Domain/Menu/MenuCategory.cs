namespace Gateway.Domain.Menu;

public sealed class MenuCategory
{
    public required string ExternalId { get; set; }
    public required string Name { get; set; }
    public List<MenuProduct> Products { get; set; } = [];
}
