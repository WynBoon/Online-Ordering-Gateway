using System.Text.Json.Serialization;

namespace Gateway.Adapters.Pilot.Dtos;

/// <summary>Mirrors <c>#/components/schemas/Pilot.OpenApiWeb.StatusResponse</c> — the
/// generic response shape for most Pilot endpoints.</summary>
public sealed class StatusResponse
{
    [JsonPropertyName("Status")]
    public bool Status { get; set; }

    [JsonPropertyName("Message")]
    public string? Message { get; set; }

    [JsonPropertyName("Code")]
    public int Code { get; set; }

    [JsonPropertyName("Reference")]
    public string? Reference { get; set; }
}
