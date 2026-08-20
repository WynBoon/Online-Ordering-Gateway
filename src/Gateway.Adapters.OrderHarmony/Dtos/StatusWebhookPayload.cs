using System.Text.Json.Serialization;

namespace Gateway.Adapters.OrderHarmony.Dtos;

/// <summary>Mirrors doc 02 §3's event envelope. <see cref="EventId"/> must be stable
/// across retries — Order Harmony dedupes on it (ARCHITECTURE.md §6).</summary>
public sealed class StatusWebhookPayload
{
    [JsonPropertyName("event_id")]
    public required string EventId { get; set; }

    [JsonPropertyName("event_type")]
    public required string EventType { get; set; } = "order.status_changed";

    [JsonPropertyName("event_time")]
    public required DateTimeOffset EventTime { get; set; }

    [JsonPropertyName("order_ref")]
    public required string OrderRef { get; set; }

    [JsonPropertyName("pos_order_id")]
    public string? PosOrderId { get; set; }

    /// <summary>accepted | preparing | ready | completed | cancelled.</summary>
    [JsonPropertyName("status")]
    public required string Status { get; set; }

    /// <summary>Only present when status is "cancelled".</summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}
