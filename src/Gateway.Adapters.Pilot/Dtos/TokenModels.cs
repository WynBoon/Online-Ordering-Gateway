using System.Text.Json.Serialization;

namespace Gateway.Adapters.Pilot.Dtos;

/// <summary>Mirrors <c>#/components/schemas/Pilot.OpenApiWeb.Models.Request.TokenRequest</c>.</summary>
public sealed class TokenRequest
{
    [JsonPropertyName("ApiKey")]
    public required string ApiKey { get; set; }

    [JsonPropertyName("Permissions")]
    public List<string>? Permissions { get; set; }
}

/// <summary>Mirrors <c>#/components/schemas/Pilot.OpenApiWeb.Models.Response.TokenResponse</c>.</summary>
public sealed class TokenResponse
{
    [JsonPropertyName("Token")]
    public required string Token { get; set; }

    [JsonPropertyName("VendorId")]
    public string? VendorId { get; set; }

    [JsonPropertyName("StoreId")]
    public string? StoreId { get; set; }

    /// <summary>Unix seconds — token is valid until this time.</summary>
    [JsonPropertyName("exp")]
    public long Exp { get; set; }
}

/// <summary>Mirrors <c>#/components/schemas/OnlineOrderLibrary.SalesProductsMenu</c>-shaped
/// response from GET /SalesProducts/Menu. Field names are a best-effort mapping — the
/// exact modifier min/max structure is unconfirmed (open question, ARCHITECTURE.md §10).</summary>
public sealed class PilotMenuProduct
{
    [JsonPropertyName("plu")]
    public required string Plu { get; set; }

    [JsonPropertyName("description")]
    public required string Description { get; set; }

    [JsonPropertyName("price")]
    public int Price { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }
}
