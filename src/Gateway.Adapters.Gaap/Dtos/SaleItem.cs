using System.Text.Json.Serialization;

namespace Gateway.Adapters.Gaap.Dtos;

/// <summary>Mirrors <c>#/definitions/Sale Item</c>.</summary>
public sealed class SaleItem
{
    /// <summary>GAAP's own product id — passed straight through as the canonical
    /// order's ExternalProductId (ARCHITECTURE.md §7, passthrough).</summary>
    [JsonPropertyName("productId")]
    public required string ProductId { get; set; }

    [JsonPropertyName("quantity")]
    public required double Quantity { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("addedTime")]
    public required string AddedTime { get; set; }

    /// <summary>Tax-inclusive price per unit.</summary>
    [JsonPropertyName("priceIncl")]
    public required double PriceIncl { get; set; }

    /// <summary>
    /// Schema requires this field but GAAP's <c>/products</c> and <c>/groups</c>
    /// endpoints don't expose what add-ons/modifiers exist — open question flagged
    /// in ARCHITECTURE.md §10 (GAAP item 5). Shape here is a best guess pending
    /// their confirmation; do not treat as verified.
    /// </summary>
    [JsonPropertyName("addOns")]
    public required List<GaapAddOn> AddOns { get; set; } = [];
}

/// <summary>Unconfirmed shape — see the warning on <see cref="SaleItem.AddOns"/>.</summary>
public sealed class GaapAddOn
{
    [JsonPropertyName("productId")]
    public required string ProductId { get; set; }

    [JsonPropertyName("quantity")]
    public double Quantity { get; set; } = 1;

    [JsonPropertyName("priceIncl")]
    public double PriceIncl { get; set; }
}
