using System.Text.Json.Serialization;

namespace Ordering.Client;

public static class GatewayEndpoints
{
    public const string UatApi = "https://moog-api-cehvddbad6c0f8gd.southafricanorth-01.azurewebsites.net";

    public static bool IsLocal(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return true;
        }

        var value = url.Trim();
        return value.Contains("localhost", StringComparison.OrdinalIgnoreCase)
               || value.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase)
               || value.Contains("10.0.2.2", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class OrderingSession
{
    public required string GatewayBaseUrl { get; set; }
    public required string LocationKey { get; set; }
    public string SourceChannel { get; set; } = "test";
    public string LocationId { get; set; } = "local-dev";
    public string Currency { get; set; } = "ZAR";
}

public interface IOrderingSessionStore
{
    Task<OrderingSession?> GetAsync(CancellationToken ct = default);
    Task SaveAsync(OrderingSession session, CancellationToken ct = default);
    Task ClearAsync(CancellationToken ct = default);
}

public sealed class MemoryOrderingSessionStore : IOrderingSessionStore
{
    private OrderingSession? _session;

    public Task<OrderingSession?> GetAsync(CancellationToken ct = default) =>
        Task.FromResult(_session);

    public Task SaveAsync(OrderingSession session, CancellationToken ct = default)
    {
        _session = session;
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken ct = default)
    {
        _session = null;
        return Task.CompletedTask;
    }
}

public sealed record HealthResult(bool Ok, string? Detail);

public sealed record InjectionResult(
    bool Success,
    string OrderRef,
    string DisplayId,
    string IdempotencyKey,
    string? PosOrderId,
    int StatusCode,
    string? ErrorCode,
    string? ErrorMessage,
    bool Retryable);

public sealed class ChannelException : Exception
{
    public ChannelException(string message) : base(message) { }
}

public sealed class MenuDto
{
    [JsonPropertyName("categories")]
    public List<MenuCategoryDto> Categories { get; set; } = [];
}

public sealed class MenuCategoryDto
{
    [JsonPropertyName("external_id")]
    public string ExternalId { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("products")]
    public List<MenuProductDto> Products { get; set; } = [];
}

public sealed class MenuProductDto
{
    [JsonPropertyName("external_id")]
    public string ExternalId { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("price_cents")]
    public long PriceCents { get; set; }

    [JsonPropertyName("tax_rate_bp")]
    public int? TaxRateBp { get; set; }

    [JsonPropertyName("modifier_groups")]
    public List<ModifierGroupDto> ModifierGroups { get; set; } = [];
}

public sealed class ModifierGroupDto
{
    [JsonPropertyName("external_id")]
    public string ExternalId { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("min_select")]
    public int MinSelect { get; set; }

    [JsonPropertyName("max_select")]
    public int MaxSelect { get; set; }

    [JsonPropertyName("modifiers")]
    public List<MenuModifierDto> Modifiers { get; set; } = [];
}

public sealed class MenuModifierDto
{
    [JsonPropertyName("external_id")]
    public string ExternalId { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("price_delta_cents")]
    public long PriceDeltaCents { get; set; }
}

public sealed class ChannelOrderDto
{
    [JsonPropertyName("order_ref")]
    public string OrderRef { get; set; } = "";

    [JsonPropertyName("display_id")]
    public string DisplayId { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("pos_order_id")]
    public string? PosOrderId { get; set; }

    [JsonPropertyName("fulfillment_type")]
    public string FulfillmentType { get; set; } = "";

    [JsonPropertyName("source_channel")]
    public string? SourceChannel { get; set; }

    [JsonPropertyName("total_cents")]
    public long TotalCents { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "ZAR";

    [JsonPropertyName("placed_at")]
    public DateTimeOffset PlacedAt { get; set; }

    [JsonPropertyName("cancel_reason")]
    public string? CancelReason { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("customer_name")]
    public string? CustomerName { get; set; }

    [JsonPropertyName("items")]
    public List<ChannelOrderItemDto> Items { get; set; } = [];

    [JsonPropertyName("events")]
    public List<ChannelOrderEventDto>? Events { get; set; }
}

public sealed class ChannelOrderItemDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("total_price_cents")]
    public long TotalPriceCents { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("modifiers")]
    public List<string> Modifiers { get; set; } = [];
}

public sealed class ChannelOrderEventDto
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("event_time")]
    public DateTimeOffset EventTime { get; set; }
}

internal sealed class OrderInjectionRequest
{
    [JsonPropertyName("order_ref")]
    public required string OrderRef { get; set; }

    [JsonPropertyName("display_id")]
    public required string DisplayId { get; set; }

    [JsonPropertyName("source_channel")]
    public required string SourceChannel { get; set; }

    [JsonPropertyName("location_id")]
    public required string LocationId { get; set; }

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

internal sealed class CustomerDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }
}

internal sealed class DeliveryAddressDto
{
    [JsonPropertyName("line1")]
    public required string Line1 { get; set; }

    [JsonPropertyName("line2")]
    public string? Line2 { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("postal_code")]
    public string? PostalCode { get; set; }
}

internal sealed class PaymentDto
{
    [JsonPropertyName("prepaid")]
    public required bool Prepaid { get; set; }
}

internal sealed class OrderItemDto
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

internal sealed class OrderModifierDto
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

internal sealed class OrderInjectionResponse
{
    [JsonPropertyName("pos_order_id")]
    public string? PosOrderId { get; set; }
}

internal sealed class ErrorEnvelope
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("retryable")]
    public bool Retryable { get; set; }
}

internal sealed class HealthDto
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }
}
