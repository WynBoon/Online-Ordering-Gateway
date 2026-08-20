using System.Text.Json.Serialization;

namespace Gateway.Adapters.Pilot.Dtos;

/// <summary>Mirrors <c>#/components/schemas/OnlineOrderLibrary.OnlineOrderRequest</c> in
/// docs/reference/pilot.swagger.json. Pilot's is a live order object with a real
/// status lifecycle and a per-order callback — the closer match to what Order
/// Harmony expects (ARCHITECTURE.md §2).</summary>
public sealed class OnlineOrderRequest
{
    [JsonPropertyName("vendorId")]
    public int VendorId { get; set; }

    [JsonPropertyName("siteId")]
    public int SiteId { get; set; }

    /// <summary>Our unique number for the order — derived from order_ref, see PilotIdempotency.
    /// The swagger declares int32 but its own example (1643825010847) overflows int32, so this
    /// is modelled as a long.</summary>
    [JsonPropertyName("orderId")]
    public long OrderId { get; set; }

    [JsonPropertyName("orderReference")]
    public string? OrderReference { get; set; }

    /// <summary>"yyyy-mm-dd HH:mm:ss" in the store's local time — Pilot expects naive
    /// local strings, not UTC (ARCHITECTURE.md §7, "Timezones").</summary>
    [JsonPropertyName("orderedDate")]
    public string? OrderedDate { get; set; }

    [JsonPropertyName("createdDate")]
    public string? CreatedDate { get; set; }

    /// <summary>Total amount of order in cents.</summary>
    [JsonPropertyName("orderAmount")]
    public int OrderAmount { get; set; }

    [JsonPropertyName("tip")]
    public int Tip { get; set; }

    [JsonPropertyName("subBrand")]
    public string? SubBrand { get; set; }

    [JsonPropertyName("orderStatus")]
    public Orderstatus? OrderStatus { get; set; }

    [JsonPropertyName("client")]
    public ClientInfo? Client { get; set; }

    [JsonPropertyName("delivery")]
    public DeliveryInfo? Delivery { get; set; }

    [JsonPropertyName("items")]
    public List<OrderItem>? Items { get; set; }

    [JsonPropertyName("payments")]
    public OrderPayments? Payments { get; set; }

    [JsonPropertyName("tableInfo")]
    public TableInfo? TableInfo { get; set; }

    /// <summary>Where Pilot pushes status updates back to us. Exact payload shape Pilot
    /// posts here is unconfirmed — open question, ARCHITECTURE.md §10.</summary>
    [JsonPropertyName("callbackUrl")]
    public string? CallbackUrl { get; set; }
}

public sealed class Orderstatus
{
    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }
}

public sealed class ClientInfo
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("contactNumber")]
    public string? ContactNumber { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }
}

public sealed class DeliveryInfo
{
    [JsonPropertyName("companyName")]
    public string? CompanyName { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("deliveryCost")]
    public int DeliveryCost { get; set; }

    [JsonPropertyName("deliveryNote")]
    public string? DeliveryNote { get; set; }

    /// <summary>"Collect", "Delivery", or "Inhouse" per the swagger example.</summary>
    [JsonPropertyName("deliveryMethod")]
    public string? DeliveryMethod { get; set; }
}

public sealed class TableInfo
{
    [JsonPropertyName("invoiceRef")]
    public string? InvoiceRef { get; set; }

    [JsonPropertyName("tableCovers")]
    public int TableCovers { get; set; }

    [JsonPropertyName("ignoreNoStock")]
    public bool IgnoreNoStock { get; set; }
}
