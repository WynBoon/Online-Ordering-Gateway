using System.Text.Json.Serialization;

namespace Gateway.Adapters.Gaap.Dtos;

/// <summary>Mirrors <c>#/definitions/Sale Payment</c>.</summary>
public sealed class SalePayment
{
    /// <summary>Which GAAP payment method represents "already paid via the online
    /// channel" — sourced from PosConnection config, pending GAAP's answer (§10).</summary>
    [JsonPropertyName("paymentMethodId")]
    public required string PaymentMethodId { get; set; }

    [JsonPropertyName("amount")]
    public required double Amount { get; set; }

    [JsonPropertyName("actualTender")]
    public required double ActualTender { get; set; }
}
