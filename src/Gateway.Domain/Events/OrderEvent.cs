using Gateway.Domain.Enums;

namespace Gateway.Domain.Events;

/// <summary>
/// Append-only status history. Source of truth for outbound-webhook
/// dedupe/replay, for audit, and for the live/historical observability views
/// (ARCHITECTURE.md §7, §12). Every meaningful step becomes one of these —
/// order received, adapter call, status transition, webhook delivered/retried,
/// store paused.
/// </summary>
public sealed class OrderEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid StoreId { get; set; }
    public required string OrderRef { get; set; }

    /// <summary>Stable across retries — outbound webhook delivery dedupes on this.</summary>
    public required string EventId { get; set; }

    public required string EventType { get; set; }
    public OrderStatus? Status { get; set; }
    public string? Outcome { get; set; }

    /// <summary>Human-readable POS / adapter detail (truncated). Not a full request/response dump.</summary>
    public string? Detail { get; set; }

    public long? DurationMs { get; set; }
    public DateTimeOffset EventTimeUtc { get; set; } = DateTimeOffset.UtcNow;
}
