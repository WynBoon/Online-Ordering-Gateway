using Gateway.Domain.Enums;

namespace Gateway.Domain.Orders;

/// <summary>
/// Modelled directly on Order Harmony's contract since it's the strictest/most
/// complete of the three integrations (ARCHITECTURE.md §4). All money fields are
/// integer minor units (cents) — never a float, never a string. All timestamps
/// are UTC internally; conversion to a store's local time happens only at the
/// POS adapter boundary (ARCHITECTURE.md §7, "Timezones").
/// </summary>
public sealed class CanonicalOrder
{
    /// <summary>Order Harmony's unique id. Used for dedupe — see <see cref="OrderRef"/> uniqueness in persistence.</summary>
    public required string OrderRef { get; set; }

    /// <summary>Short human code shown on the ticket, e.g. "A4F2".</summary>
    public required string DisplayId { get; set; }

    /// <summary>e.g. "uber_eats", "direct_dine", "test" — open-ended per the channel spec, not a closed enum.</summary>
    public required string SourceChannel { get; set; }

    /// <summary>Virtual brand for ghost kitchens. Print on the ticket if present.</summary>
    public string? BrandName { get; set; }

    /// <summary>Our internal Store id — resolved from the channel's location key, not the channel's own id.</summary>
    public required Guid StoreId { get; set; }

    public FulfillmentType FulfillmentType { get; set; }

    public DateTimeOffset PlacedAtUtc { get; set; }

    /// <summary>Present for scheduled orders. Fire at this time.</summary>
    public DateTimeOffset? ScheduledForUtc { get; set; }

    public CustomerInfo? Customer { get; set; }

    /// <summary>Required when <see cref="FulfillmentType"/> is Delivery.</summary>
    public DeliveryAddress? DeliveryAddress { get; set; }

    public List<CanonicalOrderItem> Items { get; set; } = [];

    public long SubtotalCents { get; set; }
    public long TaxCents { get; set; }

    /// <summary>0 for pickup.</summary>
    public long DeliveryFeeCents { get; set; }

    public long TipCents { get; set; }

    /// <summary>Subtotal + tax + delivery fee + tip.</summary>
    public long TotalCents { get; set; }

    /// <summary>ISO-4217, e.g. "ZAR".</summary>
    public required string Currency { get; set; }

    /// <summary>True = already paid on the channel; the till must not take payment.</summary>
    public bool Prepaid { get; set; }

    /// <summary>Order-level special instructions.</summary>
    public string? Notes { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Accepted;
    public CancelReason? CancelReason { get; set; }

    /// <summary>Set once the POS confirms creation — GAAP's invoice number, Pilot's orderId, etc.</summary>
    public string? PosOrderId { get; set; }
}
