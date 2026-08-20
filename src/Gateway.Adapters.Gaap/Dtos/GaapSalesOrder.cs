using System.Text.Json.Serialization;

namespace Gateway.Adapters.Gaap.Dtos;

/// <summary>Mirrors the confirmed fields of <c>#/definitions/Sales Order</c>, used by
/// the status synthesizer's poll (ARCHITECTURE.md §5) — GAAP gives no push
/// notification, so this is the only way to confirm a sale actually closed.</summary>
public sealed class GaapSalesOrder
{
    [JsonPropertyName("_id")]
    public required string Id { get; set; }

    [JsonPropertyName("invoiceNumber")]
    public required string InvoiceNumber { get; set; }

    /// <summary>OPEN | TENDERED | SUBTOTALED | STORED | CANCELED.</summary>
    [JsonPropertyName("status")]
    public required string Status { get; set; }
}

public sealed class GaapSalesResponse
{
    [JsonPropertyName("totalRecords")]
    public double TotalRecords { get; set; }

    [JsonPropertyName("data")]
    public List<GaapSalesOrder> Data { get; set; } = [];
}
