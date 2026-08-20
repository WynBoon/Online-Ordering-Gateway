using System.Text.Json.Serialization;

namespace Gateway.Adapters.OrderHarmony.Dtos;

/// <summary>Mirrors doc 03's menu shape — category → product → modifier group → modifier,
/// with stable external_ids that double as the mapping keys used on order injection
/// (ARCHITECTURE.md §7, passthrough).</summary>
public sealed class MenuResponseDto
{
    [JsonPropertyName("categories")]
    public required List<MenuCategoryDto> Categories { get; set; }
}

public sealed class MenuCategoryDto
{
    [JsonPropertyName("external_id")]
    public required string ExternalId { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("products")]
    public required List<MenuProductDto> Products { get; set; }
}

public sealed class MenuProductDto
{
    [JsonPropertyName("external_id")]
    public required string ExternalId { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("price_cents")]
    public required long PriceCents { get; set; }

    [JsonPropertyName("tax_rate_bp")]
    public int? TaxRateBp { get; set; }

    [JsonPropertyName("modifier_groups")]
    public required List<ModifierGroupDto> ModifierGroups { get; set; }
}

public sealed class ModifierGroupDto
{
    [JsonPropertyName("external_id")]
    public required string ExternalId { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("min_select")]
    public required int MinSelect { get; set; }

    [JsonPropertyName("max_select")]
    public required int MaxSelect { get; set; }

    [JsonPropertyName("modifiers")]
    public required List<MenuModifierDto> Modifiers { get; set; }
}

public sealed class MenuModifierDto
{
    [JsonPropertyName("external_id")]
    public required string ExternalId { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("price_delta_cents")]
    public required long PriceDeltaCents { get; set; }
}
