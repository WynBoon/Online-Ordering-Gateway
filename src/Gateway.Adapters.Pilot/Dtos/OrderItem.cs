using System.Text.Json.Serialization;

namespace Gateway.Adapters.Pilot.Dtos;

/// <summary>Mirrors <c>#/components/schemas/OnlineOrderLibrary.OrderItem</c>.</summary>
public sealed class OrderItem
{
    /// <summary>Pilot's own PLU — passed straight through as the canonical order's
    /// ExternalProductId (ARCHITECTURE.md §7, passthrough).</summary>
    [JsonPropertyName("plu")]
    public required string Plu { get; set; }

    /// <summary>Price in cents.</summary>
    [JsonPropertyName("price")]
    public int Price { get; set; }

    [JsonPropertyName("item")]
    public string? Item { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; } = 1;

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("options")]
    public List<OrderItemOption>? Options { get; set; }
}

/// <summary>Mirrors <c>#/components/schemas/OnlineOrderLibrary.OrderItemOption</c> — a modifier.</summary>
public sealed class OrderItemOption
{
    [JsonPropertyName("plu")]
    public required string Plu { get; set; }

    [JsonPropertyName("item")]
    public string? Item { get; set; }

    [JsonPropertyName("price")]
    public int Price { get; set; }
}
