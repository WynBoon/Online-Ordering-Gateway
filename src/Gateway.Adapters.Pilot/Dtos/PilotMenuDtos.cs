using System.Text.Json.Serialization;

namespace Gateway.Adapters.Pilot.Dtos;

/// <summary>Mirrors <c>#/components/schemas/PilotLiveDataAccess.MenuResponse</c>
/// from GET /SalesProducts/Menu — an object wrapping <see cref="PluItems"/>, not a raw array.</summary>
public sealed class PilotMenuResponse
{
    [JsonPropertyName("storeId")]
    public string? StoreId { get; set; }

    [JsonPropertyName("PluItems")]
    public List<PilotPluItem>? PluItems { get; set; }

    [JsonPropertyName("status")]
    public bool? Status { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>Mirrors <c>#/components/schemas/PilotLiveDataAccess.PluItem</c>.</summary>
public sealed class PilotPluItem
{
    [JsonPropertyName("Plu")]
    public string? Plu { get; set; }

    [JsonPropertyName("ItemName")]
    public string? ItemName { get; set; }

    /// <summary>Major units (e.g. 85.00 ZAR), not cents — converted at the adapter boundary.</summary>
    [JsonPropertyName("Price")]
    public double Price { get; set; }

    [JsonPropertyName("Dtab")]
    public string? Dtab { get; set; }

    [JsonPropertyName("Options")]
    public List<PilotMenuOption>? Options { get; set; }
}

/// <summary>Mirrors <c>#/components/schemas/PilotLiveDataAccess.Option</c>.</summary>
public sealed class PilotMenuOption
{
    [JsonPropertyName("OptionName")]
    public string? OptionName { get; set; }

    [JsonPropertyName("OptionItems")]
    public List<PilotMenuOptionItem>? OptionItems { get; set; }
}

/// <summary>Mirrors <c>#/components/schemas/PilotLiveDataAccess.OptionItem</c>.</summary>
public sealed class PilotMenuOptionItem
{
    [JsonPropertyName("Plu")]
    public string? Plu { get; set; }

    [JsonPropertyName("ItemName")]
    public string? ItemName { get; set; }

    [JsonPropertyName("Price")]
    public double Price { get; set; }
}
