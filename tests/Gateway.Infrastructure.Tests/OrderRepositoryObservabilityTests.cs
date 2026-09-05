using Gateway.Domain.Enums;
using Gateway.Domain.Events;
using Gateway.Domain.Orders;
using Gateway.Infrastructure.Persistence;
using Gateway.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Gateway.Infrastructure.Tests;

public class OrderRepositoryObservabilityTests
{
    private static GatewayDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<GatewayDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new GatewayDbContext(options);
    }

    [Fact]
    public async Task GetRecentEvents_orders_newest_first_and_respects_take()
    {
        await using var db = CreateDb();
        var repo = new OrderRepository(db);
        var storeId = Guid.NewGuid();

        await repo.AppendEventAsync(new OrderEvent
        {
            StoreId = storeId,
            OrderRef = "A",
            EventId = Guid.NewGuid().ToString(),
            EventType = "order.status_changed",
            Outcome = "success",
            EventTimeUtc = DateTimeOffset.UtcNow.AddMinutes(-2)
        }, CancellationToken.None);
        await repo.AppendEventAsync(new OrderEvent
        {
            StoreId = storeId,
            OrderRef = "B",
            EventId = Guid.NewGuid().ToString(),
            EventType = "order.injection_failed",
            Outcome = "pos_failure",
            EventTimeUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
        }, CancellationToken.None);

        var recent = await repo.GetRecentEventsAsync(1, CancellationToken.None);
        Assert.Single(recent);
        Assert.Equal("B", recent[0].OrderRef);
    }

    [Fact]
    public async Task GetSuccessRateLastHour_returns_ratio_or_null()
    {
        await using var db = CreateDb();
        var repo = new OrderRepository(db);
        Assert.Null(await repo.GetSuccessRateLastHourAsync(CancellationToken.None));

        var storeId = Guid.NewGuid();
        await repo.AppendEventAsync(new OrderEvent
        {
            StoreId = storeId,
            OrderRef = "1",
            EventId = Guid.NewGuid().ToString(),
            EventType = "order.status_changed",
            Outcome = "success"
        }, CancellationToken.None);
        await repo.AppendEventAsync(new OrderEvent
        {
            StoreId = storeId,
            OrderRef = "2",
            EventId = Guid.NewGuid().ToString(),
            EventType = "order.injection_failed",
            Outcome = "pos_failure"
        }, CancellationToken.None);

        var rate = await repo.GetSuccessRateLastHourAsync(CancellationToken.None);
        Assert.Equal(0.5, rate);
    }

    [Fact]
    public async Task CountOrdersToday_and_GetRecentOrders_work()
    {
        await using var db = CreateDb();
        var repo = new OrderRepository(db);
        var storeId = Guid.NewGuid();

        await repo.SaveAsync(new CanonicalOrder
        {
            OrderRef = "OH-TODAY",
            DisplayId = "T1",
            SourceChannel = "test",
            StoreId = storeId,
            Currency = "ZAR",
            PlacedAtUtc = DateTimeOffset.UtcNow,
            Status = OrderStatus.Accepted
        }, CancellationToken.None);
        await repo.SaveAsync(new CanonicalOrder
        {
            OrderRef = "OH-OLD",
            DisplayId = "T0",
            SourceChannel = "test",
            StoreId = storeId,
            Currency = "ZAR",
            PlacedAtUtc = DateTimeOffset.UtcNow.AddDays(-2),
            Status = OrderStatus.Accepted
        }, CancellationToken.None);

        Assert.Equal(1, await repo.CountOrdersTodayAsync(CancellationToken.None));
        var recent = await repo.GetRecentOrdersAsync(10, CancellationToken.None);
        Assert.Equal("OH-TODAY", recent[0].OrderRef);
        var byStore = await repo.GetRecentOrdersByStoreAsync(storeId, 10, CancellationToken.None);
        Assert.Equal(2, byStore.Count);
    }
}
