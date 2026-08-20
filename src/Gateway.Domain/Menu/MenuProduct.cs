namespace Gateway.Domain.Menu;

/// <summary>
/// <see cref="ExternalId"/> must be stable across pulls — it's the mapping key
/// used both here and on every order line (ARCHITECTURE.md §7, passthrough).
/// </summary>
public sealed class MenuProduct
{
    public required string ExternalId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public long PriceCents { get; set; }

    /// <summary>Basis points, e.g. 1500 = 15%. Optional if tax is order-level.</summary>
    public int? TaxRateBp { get; set; }

    public List<ModifierGroup> ModifierGroups { get; set; } = [];
}
