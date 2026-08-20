using Gateway.Application.Repositories;
using Gateway.Domain.Outbox;
using Gateway.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Gateway.Infrastructure.Repositories;

public sealed class OutboxRepository(GatewayDbContext db) : IOutboxRepository
{
    public async Task EnqueueAsync(OutboxMessage message, CancellationToken ct)
    {
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetUndispatchedAsync(int batchSize, CancellationToken ct) =>
        await db.OutboxMessages
            .Where(m => m.DispatchedAtUtc == null)
            .OrderBy(m => m.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(ct);

    public async Task MarkDispatchedAsync(Guid id, CancellationToken ct)
    {
        var message = await db.OutboxMessages.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (message is not null)
        {
            message.DispatchedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }
}
