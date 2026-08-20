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
}
