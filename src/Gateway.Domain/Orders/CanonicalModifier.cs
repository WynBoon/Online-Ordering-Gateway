namespace Gateway.Domain.Orders;

/// <summary>
/// <see cref="ExternalModifierId"/> IS the POS's own native modifier id (GAAP's
/// add-on id, Pilot's option <c>plu</c>) passed straight through — there is no
/// internally-invented id scheme sitting between the channel and the POS.
/// See ARCHITECTURE.md §7, "No persisted product-mapping table".
/// </summary>
public sealed class CanonicalModifier
{
    public required string ExternalModifierId { get; set; }
    public string? GroupExternalId { get; set; }
    public required string Name { get; set; }
    public int Quantity { get; set; } = 1;

    /// <summary>May be zero or negative.</summary>
    public long PriceDeltaCents { get; set; }
}
