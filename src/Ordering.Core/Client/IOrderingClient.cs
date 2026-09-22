namespace Ordering.Client;

public interface IOrderingClient
{
    Task<HealthResult> GetHealthAsync(CancellationToken ct = default);
    Task<MenuDto> GetMenuAsync(CancellationToken ct = default);
    Task<InjectionResult> PlaceOrderAsync(PlaceOrderCommand command, CancellationToken ct = default);
    Task<ChannelOrderDto> GetOrderAsync(string orderRef, CancellationToken ct = default);
    Task<IReadOnlyList<ChannelOrderDto>> ListOrdersAsync(CancellationToken ct = default);
}

public sealed class PlaceOrderCommand
{
    public required string OrderRef { get; set; }
    public required string DisplayId { get; set; }
    public required string FulfillmentType { get; set; }
    public DateTimeOffset? ScheduledFor { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }
    public string? DeliveryLine1 { get; set; }
    public string? DeliveryCity { get; set; }
    public string? DeliveryPostalCode { get; set; }
    public required IReadOnlyList<Cart.CartLine> Items { get; set; }
    public long SubtotalCents { get; set; }
    public long TaxCents { get; set; }
    public long DeliveryFeeCents { get; set; }
    public long TipCents { get; set; }
    public long TotalCents { get; set; }
    public string Currency { get; set; } = "ZAR";
    public string? Notes { get; set; }

    /// <summary>When set, reuse this key (idempotent replay). Otherwise a new GUID is issued.</summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>Override product id on the first line — Lab unknown-PLU scenario.</summary>
    public string? UnknownPluOverride { get; set; }
}
