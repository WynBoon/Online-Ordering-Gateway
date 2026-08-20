namespace Gateway.Domain.Menu;

/// <summary>
/// Reshaped from whatever the POS's catalogue looks like into the
/// category/product/modifier-group tree Order Harmony expects
/// (ARCHITECTURE.md §7). This reshaping happens on every pull — there is no
/// persisted mapping table behind it.
/// </summary>
public sealed class CanonicalMenu
{
    public required Guid StoreId { get; set; }
    public List<MenuCategory> Categories { get; set; } = [];
}
