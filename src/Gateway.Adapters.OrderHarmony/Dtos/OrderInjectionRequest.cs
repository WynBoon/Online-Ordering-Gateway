using System.Text.Json.Serialization;

namespace Gateway.Adapters.OrderHarmony.Dtos;

/// <summary>Mirrors doc 01 "Order Injection" exactly — field names are the wire
/// contract, snake_case per Order Harmony's spec, not our own convention.</summary>
public sealed class OrderInjectionRequest
{
    [JsonPropertyName("order_ref")]
    public required string OrderRef { get; set; }

    [JsonPropertyName("display_id")]
    public required string DisplayId { get; set; }

    /// <summary>"uber_eats" | "direct_dine" | "test" | others per doc 01.</summary>
    [JsonPropertyName("source_channel")]
    public required string SourceChannel { get; set; }

    [JsonPropertyName("brand_name")]
    public string? BrandName { get; set; }

    /// <summary>Our identifier for the site/till — resolved via the Bearer location
    /// key at auth time, not trusted from this field alone.</summary>
    [JsonPropertyName("location_id")]
    public required string LocationId { get; set; }

    /// <summary>"delivery" | "pickup" | "dine_in".</summary>
    [JsonPropertyName("fulfillment_type")]
    public required string FulfillmentType { get; set; }

    [JsonPropertyName("placed_at")]
    public required DateTimeOffset PlacedAt { get; set; }

    [JsonPropertyName("scheduled_for")]
    public DateTimeOffset? ScheduledFor { get; set; }

    [JsonPropertyName("customer")]
    public CustomerDto? Customer { get; set; }

    [JsonPropertyName("delivery_address")]
    public DeliveryAddressDto? DeliveryAddress { get; set; }

    [JsonPropertyName("items")]
    public required List<OrderItemDto> Items { get; set; }

    [JsonPropertyName("subtotal_cents")]
    public required long SubtotalCents { get; set; }

    [JsonPropertyName("tax_cents")]
    public required long TaxCents { get; set; }

    [JsonPropertyName("delivery_fee_cents")]
    public required long DeliveryFeeCents { get; set; }

    [JsonPropertyName("tip_cents")]
    public required long TipCents { get; set; }

    [JsonPropertyName("total_cents")]
    public required long TotalCents { get; set; }

    [JsonPropertyName("currency")]
    public required string Currency { get; set; }

    [JsonPropertyName("payment")]
    public required PaymentDto Payment { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

public sealed class CustomerDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }
}

public sealed class DeliveryAddressDto
{
    [JsonPropertyName("line1")]
    public required string Line1 { get; set; }

    [JsonPropertyName("line2")]
    public string? Line2 { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("postal_code")]
    public string? PostalCode { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

public sealed class PaymentDto
{
    /// <summary>True = already paid on the channel; the till must not take payment.</summary>
    [JsonPropertyName("prepaid")]
    public required bool Prepaid { get; set; }
}

public sealed class OrderItemDto
{
    [JsonPropertyName("external_product_id")]
    public required string ExternalProductId { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("quantity")]
    public required int Quantity { get; set; }

    [JsonPropertyName("unit_price_cents")]
    public required long UnitPriceCents { get; set; }

    [JsonPropertyName("total_price_cents")]
    public required long TotalPriceCents { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("modifiers")]
    public required List<OrderModifierDto> Modifiers { get; set; }
}

public sealed class OrderModifierDto
{
    [JsonPropertyName("external_modifier_id")]
    public required string ExternalModifierId { get; set; }

    [JsonPropertyName("group_external_id")]
    public string? GroupExternalId { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("quantity")]
    public required int Quantity { get; set; }

    [JsonPropertyName("price_delta_cents")]
    public required long PriceDeltaCents { get; set; }
}

public sealed class OrderInjectionResponse
{
    [JsonPropertyName("pos_order_id")]
    public required string PosOrderId { get; set; }
}
