using Gateway.Application.Repositories;
using Gateway.Domain.Tenancy;
using Gateway.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Gateway.Infrastructure.Repositories;

public sealed class StoreRepository(GatewayDbContext db) : IStoreRepository
{
    public Task<Store?> GetByIdAsync(Guid storeId, CancellationToken ct) =>
        db.Stores.FirstOrDefaultAsync(s => s.Id == storeId, ct);

    public async Task<Store?> GetByLocationKeyAsync(string locationKey, CancellationToken ct)
    {
        var connection = await db.ChannelConnections.FirstOrDefaultAsync(
            c => c.LocationKey == locationKey || c.PreviousLocationKey == locationKey, ct);
        // Old key still accepted for 24h after rotation (doc 04 §1).
        if (connection is not null && connection.PreviousLocationKey == locationKey &&
            connection.LocationKeyRotatedAtUtc is { } rotatedAt && rotatedAt.AddHours(24) < DateTimeOffset.UtcNow)
        {
            return null;
        }

        return connection is null ? null : await db.Stores.FirstOrDefaultAsync(s => s.Id == connection.StoreId, ct);
    }

    public Task<ChannelConnection?> GetChannelConnectionAsync(Guid storeId, CancellationToken ct) =>
        db.ChannelConnections.FirstOrDefaultAsync(c => c.StoreId == storeId, ct);

    public Task<PosConnection?> GetPosConnectionAsync(Guid storeId, CancellationToken ct) =>
        db.PosConnections.FirstOrDefaultAsync(c => c.StoreId == storeId, ct);

    public async Task<IReadOnlyList<Store>> GetActiveStoresAsync(CancellationToken ct) =>
        await db.Stores.Where(s => s.State == Domain.Enums.StoreState.Active).ToListAsync(ct);

    public async Task<IReadOnlyList<Store>> GetAllAsync(CancellationToken ct) =>
        await db.Stores.OrderBy(s => s.Name).ToListAsync(ct);

    public async Task CreateAsync(Store store, CancellationToken ct)
    {
        db.Stores.Add(store);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateStateAsync(Guid storeId, Domain.Enums.StoreState newState, string? reason, CancellationToken ct)
    {
        var store = await db.Stores.FirstOrDefaultAsync(s => s.Id == storeId, ct);
        if (store is null)
        {
            return;
        }

        store.State = newState;
        store.StateChangedAtUtc = DateTimeOffset.UtcNow;
        store.StateChangeReason = reason;
        await db.SaveChangesAsync(ct);
    }
}
