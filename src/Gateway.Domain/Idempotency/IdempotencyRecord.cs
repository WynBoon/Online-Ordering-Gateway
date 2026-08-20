namespace Gateway.Domain.Idempotency;

/// <summary>
/// Caches the exact response returned for an Order Harmony <c>Idempotency-Key</c>,
/// so a replayed request gets the original 200/201 verbatim instead of being
/// re-processed (ARCHITECTURE.md §6). TTL ≥ 24h per the channel spec's dedupe window.
/// </summary>
public sealed class IdempotencyRecord
{
    public required string IdempotencyKey { get; set; }
    public required int ResponseStatusCode { get; set; }
    public required string ResponseBodyJson { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAtUtc { get; set; } = DateTimeOffset.UtcNow.AddHours(24);
}
