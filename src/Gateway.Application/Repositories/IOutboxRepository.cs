using Gateway.Domain.Outbox;

namespace Gateway.Application.Repositories;

public interface IOutboxRepository
{
    /// <summary>Writes the message in the same transaction as the caller's other changes
    /// (the caller controls the transaction boundary via a shared unit of work — see
    /// Gateway.Infrastructure's DbContext-backed implementation).</summary>
    Task EnqueueAsync(OutboxMessage message, CancellationToken ct);

    Task<IReadOnlyList<OutboxMessage>> GetUndispatchedAsync(int batchSize, CancellationToken ct);
    Task MarkDispatchedAsync(Guid id, CancellationToken ct);
}
