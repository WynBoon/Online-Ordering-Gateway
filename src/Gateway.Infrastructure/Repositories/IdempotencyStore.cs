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

    public async Task RemoveAsync(string idempotencyKey, CancellationToken ct)
    {
        var existing = await db.IdempotencyRecords.FirstOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey, ct);
        if (existing is not null)
        {
            db.IdempotencyRecords.Remove(existing);
            await db.SaveChangesAsync(ct);
        }
    }
}
