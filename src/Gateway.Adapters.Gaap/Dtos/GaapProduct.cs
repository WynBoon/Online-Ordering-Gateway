using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gateway.Adapters.Gaap.Dtos;

/// <summary>
/// Mirrors the confirmed fields of <c>#/definitions/Products</c> in
/// docs/reference/gaap.swagger.json. <see cref="Pricing"/> is left raw —
/// GAAP's pricing schema (price options, cost basis) is deep enough that it
/// needs mapping against real sandbox data rather than a guessed shape here.
/// TODO once sandbox access exists: replace with a typed price-extraction.
/// </summary>
public sealed class GaapProduct
{
    [JsonPropertyName("_id")]
    public required string Id { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("status")]
    public bool Active { get; set; } = true;

    [JsonPropertyName("kitchenDescription")]
    public string? KitchenDescription { get; set; }

    [JsonPropertyName("pricing")]
    public JsonElement? Pricing { get; set; }
}

public sealed class GaapProductRecordsResponse
{
    [JsonPropertyName("totalRecords")]
    public double TotalRecords { get; set; }

    [JsonPropertyName("data")]
    public List<GaapProduct> Data { get; set; } = [];
}

/// <summary>Unconfirmed shape for <c>/groups</c> (category) records — GAAP's swagger
/// doesn't expand this definition beyond the list wrapper. Flagged for verification.</summary>
public sealed class GaapProductGroup
{
    [JsonPropertyName("_id")]
    public required string Id { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }
}

public sealed class GaapProductGroupsResponse
{
    [JsonPropertyName("totalRecords")]
    public double TotalRecords { get; set; }

    [JsonPropertyName("data")]
    public List<GaapProductGroup> Data { get; set; } = [];
}
