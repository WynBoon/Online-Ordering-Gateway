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

    [JsonPropertyName("TokenType")]
    public string? TokenType { get; set; }

    [JsonPropertyName("VendorId")]
    public string? VendorId { get; set; }

    [JsonPropertyName("StoreId")]
    public string? StoreId { get; set; }

    [JsonPropertyName("Permissions")]
    public List<string>? Permissions { get; set; }

    [JsonPropertyName("nbf")]
    public long Nbf { get; set; }

    /// <summary>Unix seconds — token is valid until this time.</summary>
    [JsonPropertyName("exp")]
    public long Exp { get; set; }
}
