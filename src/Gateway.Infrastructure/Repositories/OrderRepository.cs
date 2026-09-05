using Gateway.Application.Repositories;
using Gateway.Domain.Enums;
using Gateway.Domain.Events;
using Gateway.Domain.Orders;
using Gateway.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Gateway.Infrastructure.Repositories;

public sealed class OrderRepository(GatewayDbContext db) : IOrderRepository
{
    public Task<CanonicalOrder?> GetByOrderRefAsync(string orderRef, CancellationToken ct) =>
        db.Orders.FirstOrDefaultAsync(o => o.OrderRef == orderRef, ct);

    public async Task SaveAsync(CanonicalOrder order, CancellationToken ct)
    {
        var existing = await db.Orders.FirstOrDefaultAsync(o => o.OrderRef == order.OrderRef, ct);
        if (existing is null)
        {
            db.Orders.Add(order);
        }
        else
        {
            db.Entry(existing).CurrentValues.SetValues(order);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task AppendEventAsync(OrderEvent orderEvent, CancellationToken ct)
    {
        db.OrderEvents.Add(orderEvent);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CanonicalOrder>> GetPendingByPosTypeAsync(PosType posType, CancellationToken ct)
    {
        var storeIds = await db.PosConnections
            .Where(c => c.PosType == posType)
            .Select(c => c.StoreId)
            .ToListAsync(ct);

        return await db.Orders
            .Where(o => storeIds.Contains(o.StoreId) && o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<OrderEvent>> GetRecentEventsAsync(int take, CancellationToken ct) =>
        await db.OrderEvents
            .OrderByDescending(e => e.EventTimeUtc)
            .Take(take)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<OrderEvent>> GetRecentEventsByStoreAsync(Guid storeId, int take, CancellationToken ct) =>
        await db.OrderEvents
            .Where(e => e.StoreId == storeId)
            .OrderByDescending(e => e.EventTimeUtc)
            .Take(take)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<OrderEvent>> GetEventsByOrderRefAsync(string orderRef, CancellationToken ct) =>
        await db.OrderEvents
            .Where(e => e.OrderRef == orderRef)
            .OrderBy(e => e.EventTimeUtc)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CanonicalOrder>> GetRecentOrdersAsync(int take, CancellationToken ct) =>
        await db.Orders
            .OrderByDescending(o => o.PlacedAtUtc)
            .Take(take)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CanonicalOrder>> GetRecentOrdersByStoreAsync(Guid storeId, int take, CancellationToken ct) =>
        await db.Orders
            .Where(o => o.StoreId == storeId)
            .OrderByDescending(o => o.PlacedAtUtc)
            .Take(take)
            .ToListAsync(ct);

    public async Task<int> CountOrdersTodayAsync(CancellationToken ct)
    {
        var start = DateTimeOffset.UtcNow.Date;
        var end = start.AddDays(1);
        return await db.Orders.CountAsync(o => o.PlacedAtUtc >= start && o.PlacedAtUtc < end, ct);
    }

    public async Task<double?> GetSuccessRateLastHourAsync(CancellationToken ct)
    {
        var since = DateTimeOffset.UtcNow.AddHours(-1);
        var events = await db.OrderEvents
            .Where(e => e.EventTimeUtc >= since
                        && (e.EventType == "order.status_changed" || e.EventType == "order.injection_failed"))
            .Select(e => e.Outcome)
            .ToListAsync(ct);

        if (events.Count == 0)
        {
            return null;
        }

        var successes = events.Count(o => string.Equals(o, "success", StringComparison.OrdinalIgnoreCase));
        return (double)successes / events.Count;
    }
}
