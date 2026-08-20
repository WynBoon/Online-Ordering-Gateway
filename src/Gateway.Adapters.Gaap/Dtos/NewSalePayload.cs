using System.Text.Json.Serialization;

namespace Gateway.Adapters.Gaap.Dtos;

/// <summary>
/// Mirrors <c>#/definitions/NewSalePayload</c> in docs/reference/gaap.swagger.json
/// exactly. GAAP models a sale as an already-closed, already-paid transaction —
/// there is no "pending order" concept, which is why every online order is
/// submitted with <see cref="Status"/> = TENDERED at injection time
/// (ARCHITECTURE.md §2, §5).
/// </summary>
public sealed class NewSalePayload
{
    /// <summary>UUIDv4, deterministically derived from our order_ref — see
    /// GaapIdempotency.DeriveTransactionId. This is what GAAP dedupes on.</summary>
    [JsonPropertyName("externalTransactionId")]
    public required string ExternalTransactionId { get; set; }

    [JsonPropertyName("nodeId")]
    public required string NodeId { get; set; }

    [JsonPropertyName("terminalId")]
    public required double TerminalId { get; set; }

    [JsonPropertyName("invoiceNumber")]
    public required string InvoiceNumber { get; set; }

    [JsonPropertyName("salesOrderMethodId")]
    public string? SalesOrderMethodId { get; set; }

    [JsonPropertyName("orderNum")]
    public string? OrderNum { get; set; }

    [JsonPropertyName("salesName")]
    public string? SalesName { get; set; }

    [JsonPropertyName("isNegative")]
    public required bool IsNegative { get; set; }

    /// <summary>"TENDERED" or "CANCELED" — see <see cref="GaapSaleStatus"/>.</summary>
    [JsonPropertyName("status")]
    public required string Status { get; set; }

    /// <summary>ISO date/time in the store's local time, no offset — GAAP expects naive
    /// local strings, not UTC (ARCHITECTURE.md §7, "Timezones").</summary>
    [JsonPropertyName("createdDate")]
    public required string CreatedDate { get; set; }

    [JsonPropertyName("closedDate")]
    public required string ClosedDate { get; set; }

    [JsonPropertyName("locationId")]
    public required string LocationId { get; set; }

    /// <summary>Which GAAP user online orders are posted as — sourced from PosConnection
    /// config, pending GAAP's answer on how this should be obtained (open question, §10).</summary>
    [JsonPropertyName("employeeId")]
    public required string EmployeeId { get; set; }

    [JsonPropertyName("invoiceTotal")]
    public required double InvoiceTotal { get; set; }

    [JsonPropertyName("discountsTotal")]
    public required double DiscountsTotal { get; set; }

    [JsonPropertyName("paymentsTotal")]
    public required double PaymentsTotal { get; set; }

    [JsonPropertyName("tipsTotal")]
    public required double TipsTotal { get; set; }

    [JsonPropertyName("changeGiven")]
    public required double ChangeGiven { get; set; }

    [JsonPropertyName("turnover")]
    public required double Turnover { get; set; }

    [JsonPropertyName("tax")]
    public required double Tax { get; set; }

    [JsonPropertyName("payments")]
    public required List<SalePayment> Payments { get; set; }

    [JsonPropertyName("items")]
    public required List<SaleItem> Items { get; set; }
}

public static class GaapSaleStatus
{
    public const string Tendered = "TENDERED";
    public const string Canceled = "CANCELED";
}
