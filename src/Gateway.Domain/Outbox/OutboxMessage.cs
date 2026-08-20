namespace Gateway.Domain.Outbox;

/// <summary>
/// Written in the same DB transaction as the state change it describes, then
/// published to Service Bus by a separate dispatcher. Closes the one gap
/// at-least-once Service Bus delivery doesn't cover on its own: a DB commit
/// succeeding while the message publish fails (ARCHITECTURE.md §14).
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Becomes the Service Bus SessionId — session-enabled queues key on this
    /// (usually a StoreId) so two messages for the same store are never processed
    /// out of order or concurrently (ARCHITECTURE.md §14).</summary>
    public required string SessionId { get; set; }

    public required string MessageType { get; set; }
    public required string PayloadJson { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DispatchedAtUtc { get; set; }
}
