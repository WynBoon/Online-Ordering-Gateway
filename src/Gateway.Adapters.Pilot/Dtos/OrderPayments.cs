using System.Text.Json.Serialization;

namespace Gateway.Adapters.Pilot.Dtos;

/// <summary>Mirrors <c>#/components/schemas/OnlineOrderLibrary.OrderPayments</c>.</summary>
public sealed class OrderPayments
{
    /// <summary>"PAID" or "UNPAID". Order Harmony orders always arrive prepaid, so this
    /// is always "PAID" — see PilotPaymentMapping.</summary>
    [JsonPropertyName("status")]
    public required string Status { get; set; }

    [JsonPropertyName("amount")]
    public int Amount { get; set; }

    [JsonPropertyName("payment")]
    public List<OrderPayment>? Payment { get; set; }
}

/// <summary>Mirrors <c>#/components/schemas/OnlineOrderLibrary.OrderPayment</c>.</summary>
public sealed class OrderPayment
{
    [JsonPropertyName("paymentDate")]
    public string? PaymentDate { get; set; }

    /// <summary>"Cash", "CreditCard", or "EFT" per the swagger example — no value confirmed
    /// yet for "already settled externally" (open question, ARCHITECTURE.md §10).</summary>
    [JsonPropertyName("paymentMethod")]
    public required string PaymentMethod { get; set; }

    [JsonPropertyName("amount")]
    public int Amount { get; set; }

    [JsonPropertyName("reference")]
    public string? Reference { get; set; }
}
