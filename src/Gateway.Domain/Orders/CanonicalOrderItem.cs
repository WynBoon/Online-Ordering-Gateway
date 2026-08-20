namespace Gateway.Domain.Orders;

/// <summary>
/// <see cref="ExternalProductId"/> IS the POS's own native product id (GAAP
/// <c>productId</c>, Pilot <c>plu</c>) passed straight through both directions —
/// see ARCHITECTURE.md §7, "No persisted product-mapping table".
/// </summary>
public sealed class CanonicalOrderItem
{
    public required string ExternalProductId { get; set; }

    /// <summary>Display name at time of order — not necessarily the POS's current name for it.</summary>
    public required string Name { get; set; }

    public int Quantity { get; set; } = 1;

    /// <summary>Excluding modifiers.</summary>
    public long UnitPriceCents { get; set; }

    /// <summary>Line total including modifiers times quantity.</summary>
    public long TotalPriceCents { get; set; }

    public string? Notes { get; set; }

    public List<CanonicalModifier> Modifiers { get; set; } = [];
}
