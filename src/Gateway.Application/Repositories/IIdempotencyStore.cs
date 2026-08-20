using Gateway.Domain.Idempotency;

namespace Gateway.Application.Repositories;

public interface IIdempotencyStore
{
    Task<IdempotencyRecord?> FindAsync(string idempotencyKey, CancellationToken ct);
    Task SaveAsync(IdempotencyRecord record, CancellationToken ct);
}
