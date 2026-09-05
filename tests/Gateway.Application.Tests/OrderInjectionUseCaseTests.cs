using Gateway.Application.Ports;
using Gateway.Application.Repositories;
using Gateway.Application.UseCases;
using Gateway.Domain.Enums;
using Gateway.Domain.Events;
using Gateway.Domain.Orders;
using Gateway.Domain.Outbox;
using Gateway.Domain.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Gateway.Application.Tests;

public class OrderInjectionUseCaseTests
{
    private static CanonicalOrder MakeOrder(Guid storeId) => new()
    {
        OrderRef = "OH-123",
        DisplayId = "A4F2",
        SourceChannel = "direct_dine",
        StoreId = storeId,
        FulfillmentType = FulfillmentType.Pickup,
        Currency = "ZAR"
    };

    [Theory]
    [InlineData(StoreState.Draft)]
    [InlineData(StoreState.Paused)]
    [InlineData(StoreState.Deactivated)]
    public async Task Rejects_injection_when_store_is_not_active(StoreState state)
    {
        var storeId = Guid.NewGuid();
        var store = new Store { Id = storeId, Name = "Test Store", Timezone = "Africa/Johannesburg", State = state };

        var storeRepo = new Mock<IStoreRepository>();
        storeRepo.Setup(r => r.GetByIdAsync(storeId, It.IsAny<CancellationToken>())).ReturnsAsync(store);

        var services = new ServiceCollection().BuildServiceProvider();
        var useCase = new OrderInjectionUseCase(
            storeRepo.Object,
            Mock.Of<IOrderRepository>(),
            Mock.Of<IOutboxRepository>(),
            services,
            NullLogger<OrderInjectionUseCase>.Instance);

        var result = await useCase.ExecuteAsync(MakeOrder(storeId), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("store_not_active", result.ErrorCode);
        Assert.False(result.Retryable);
        storeRepo.Verify(r => r.GetPosConnectionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Resolves_adapter_by_pos_type_and_persists_on_success()
    {
        var storeId = Guid.NewGuid();
        var store = new Store { Id = storeId, Name = "Test Store", Timezone = "Africa/Johannesburg", State = StoreState.Active };
        var connection = new PosConnection { StoreId = storeId, PosType = PosType.Pilot, SecretRef = "kv://pilot-key" };

        var storeRepo = new Mock<IStoreRepository>();
        storeRepo.Setup(r => r.GetByIdAsync(storeId, It.IsAny<CancellationToken>())).ReturnsAsync(store);
        storeRepo.Setup(r => r.GetPosConnectionAsync(storeId, It.IsAny<CancellationToken>())).ReturnsAsync(connection);

        var pilotAdapter = new Mock<IPosOrderAdapter>();
        pilotAdapter
            .Setup(a => a.CreateOrderAsync(It.IsAny<CanonicalOrder>(), connection, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PosOrderResult.Ok("PILOT-987", detail: "posOrderId=PILOT-987; code=0"));

        var services = new ServiceCollection()
            .AddKeyedSingleton(PosType.Pilot, pilotAdapter.Object)
            .BuildServiceProvider();

        var orderRepo = new Mock<IOrderRepository>();
        var outboxRepo = new Mock<IOutboxRepository>();

        var useCase = new OrderInjectionUseCase(
            storeRepo.Object, orderRepo.Object, outboxRepo.Object, services, NullLogger<OrderInjectionUseCase>.Instance);

        var result = await useCase.ExecuteAsync(MakeOrder(storeId), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("PILOT-987", result.PosOrderId);
        orderRepo.Verify(r => r.SaveAsync(It.Is<CanonicalOrder>(o => o.Status == OrderStatus.Accepted), It.IsAny<CancellationToken>()), Times.Once);
        orderRepo.Verify(r => r.AppendEventAsync(It.Is<OrderEvent>(e =>
            e.EventType == "order.status_changed"
            && e.Outcome == "success"
            && e.DurationMs != null
            && e.Detail != null
            && e.Detail.Contains("PILOT-987")), It.IsAny<CancellationToken>()), Times.Once);
        outboxRepo.Verify(r => r.EnqueueAsync(It.Is<OutboxMessage>(m => m.MessageType == "order.status_changed"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task On_failure_appends_event_with_detail_and_enqueues_outbox()
    {
        var storeId = Guid.NewGuid();
        var store = new Store { Id = storeId, Name = "Test Store", Timezone = "Africa/Johannesburg", State = StoreState.Active };
        var connection = new PosConnection { StoreId = storeId, PosType = PosType.Pilot, SecretRef = "kv://pilot-key" };

        var storeRepo = new Mock<IStoreRepository>();
        storeRepo.Setup(r => r.GetByIdAsync(storeId, It.IsAny<CancellationToken>())).ReturnsAsync(store);
        storeRepo.Setup(r => r.GetPosConnectionAsync(storeId, It.IsAny<CancellationToken>())).ReturnsAsync(connection);

        var pilotAdapter = new Mock<IPosOrderAdapter>();
        pilotAdapter
            .Setup(a => a.CreateOrderAsync(It.IsAny<CanonicalOrder>(), connection, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PosOrderResult.Fail("pos_failure", "till offline", retryable: true, detail: "HTTP 503"));

        var services = new ServiceCollection()
            .AddKeyedSingleton(PosType.Pilot, pilotAdapter.Object)
            .BuildServiceProvider();

        var orderRepo = new Mock<IOrderRepository>();
        var outboxRepo = new Mock<IOutboxRepository>();

        var useCase = new OrderInjectionUseCase(
            storeRepo.Object, orderRepo.Object, outboxRepo.Object, services, NullLogger<OrderInjectionUseCase>.Instance);

        var result = await useCase.ExecuteAsync(MakeOrder(storeId), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("pos_failure", result.ErrorCode);
        orderRepo.Verify(r => r.SaveAsync(It.IsAny<CanonicalOrder>(), It.IsAny<CancellationToken>()), Times.Never);
        orderRepo.Verify(r => r.AppendEventAsync(It.Is<OrderEvent>(e =>
            e.EventType == "order.injection_failed"
            && e.Outcome == "pos_failure"
            && e.Detail == "HTTP 503"
            && e.DurationMs != null), It.IsAny<CancellationToken>()), Times.Once);
        outboxRepo.Verify(r => r.EnqueueAsync(It.Is<OutboxMessage>(m => m.MessageType == "order.injection_failed"), It.IsAny<CancellationToken>()), Times.Once);
    }
}
