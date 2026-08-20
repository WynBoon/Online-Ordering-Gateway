using Gateway.Application.Repositories;
using Gateway.Domain.Idempotency;
using Gateway.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Gateway.Infrastructure.Repositories;

public sealed class IdempotencyStore(GatewayDbContext db) : IIdempotencyStore
{
    public Task<IdempotencyRecord?> FindAsync(string idempotencyKey, CancellationToken ct) =>
        db.IdempotencyRecords.FirstOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey && r.ExpiresAtUtc > DateTimeOffset.UtcNow, ct);

    public async Task SaveAsync(IdempotencyRecord record, CancellationToken ct)
    {
        db.IdempotencyRecords.Add(record);
        await db.SaveChangesAsync(ct);
    }
}
