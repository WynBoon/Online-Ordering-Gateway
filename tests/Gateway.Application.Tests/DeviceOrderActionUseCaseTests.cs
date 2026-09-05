using Gateway.Application.Repositories;
using Gateway.Application.UseCases;
using Gateway.Domain.Devices;
using Gateway.Domain.Enums;
using Gateway.Domain.Events;
using Gateway.Domain.Orders;
using Gateway.Domain.Outbox;
using Gateway.Domain.Tenancy;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Gateway.Application.Tests;

public class DeviceOrderActionUseCaseTests
{
    [Fact]
    public async Task Preparing_rejected_when_device_only_has_MarkReady_does_not_save_or_enqueue()
    {
        var (useCase, device, orderRepo, outboxRepo) = Harness(
            DeviceFunction.MarkReady,
            OrderStatus.Accepted);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            useCase.ApplyAsync(device.Id, "OH-123", DeviceOrderActionKind.Preparing, null, null, CancellationToken.None));

        Assert.Contains("MarkPreparing", ex.Message);
        orderRepo.Verify(r => r.SaveAsync(It.IsAny<CanonicalOrder>(), It.IsAny<CancellationToken>()), Times.Never);
        outboxRepo.Verify(r => r.EnqueueAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Ready_when_granted_from_Accepted_walks_ladder_and_records_device_source()
    {
        var (useCase, device, orderRepo, outboxRepo) = Harness(
            DeviceFunction.MarkReady,
            OrderStatus.Accepted);

        await useCase.ApplyAsync(device.Id, "OH-123", DeviceOrderActionKind.Ready, null, null, CancellationToken.None);

        orderRepo.Verify(
            r => r.SaveAsync(It.Is<CanonicalOrder>(o => o.Status == OrderStatus.Ready), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
        outboxRepo.Verify(
            r => r.EnqueueAsync(
                It.Is<OutboxMessage>(m => m.MessageType == "order.status_changed"),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
        orderRepo.Verify(
            r => r.AppendEventAsync(
                It.Is<OrderEvent>(e => e.Detail != null && e.Detail.Contains(device.Id.ToString())),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Map_without_view_flags_keeps_name_and_omits_phone_and_notes()
    {
        var storeId = Guid.NewGuid();
        var device = ActiveDevice(storeId, DeviceFunction.MarkReady);
        var store = ActiveStore(storeId);

        var order = new CanonicalOrder
        {
            OrderRef = "OH-123",
            DisplayId = "A4F2",
            SourceChannel = "direct_dine",
            StoreId = storeId,
            Currency = "ZAR",
            Status = OrderStatus.Accepted,
            Notes = "no onions",
            Customer = new CustomerInfo { Name = "Ada", Phone = "+27111", Email = "ada@example.com" },
            Items =
            [
                new CanonicalOrderItem
                {
                    ExternalProductId = "p1",
                    Name = "Burger",
                    Quantity = 1,
                    Notes = "extra sauce"
                }
            ]
        };

        var deviceRepo = new Mock<IStoreDeviceRepository>();
        deviceRepo.Setup(r => r.GetByIdAsync(device.Id, It.IsAny<CancellationToken>())).ReturnsAsync(device);

        var storeRepo = new Mock<IStoreRepository>();
        storeRepo.Setup(r => r.GetByIdAsync(storeId, It.IsAny<CancellationToken>())).ReturnsAsync(store);

        var orderRepo = new Mock<IOrderRepository>();
        orderRepo
            .Setup(r => r.GetOpenOrdersByStoreAsync(storeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([order]);

        var statusSync = new StatusSyncUseCase(
            orderRepo.Object, Mock.Of<IOutboxRepository>(), NullLogger<StatusSyncUseCase>.Instance);
        var useCase = new DeviceOrderActionUseCase(deviceRepo.Object, storeRepo.Object, orderRepo.Object, statusSync);

        var listed = await useCase.ListOpenOrdersAsync(device.Id, CancellationToken.None);
        var dto = Assert.Single(listed);

        Assert.Equal("Ada", dto.CustomerName);
        Assert.Null(dto.CustomerPhone);
        Assert.Null(dto.CustomerEmail);
        Assert.Null(dto.Notes);
        Assert.Null(Assert.Single(dto.Items).Notes);
    }

    private static (DeviceOrderActionUseCase UseCase, StoreDevice Device, Mock<IOrderRepository> Orders, Mock<IOutboxRepository> Outbox)
        Harness(DeviceFunction functions, OrderStatus status)
    {
        var storeId = Guid.NewGuid();
        var device = ActiveDevice(storeId, functions);
        var store = ActiveStore(storeId);
        var order = new CanonicalOrder
        {
            OrderRef = "OH-123",
            DisplayId = "A4F2",
            SourceChannel = "direct_dine",
            StoreId = storeId,
            Currency = "ZAR",
            Status = status
        };

        var deviceRepo = new Mock<IStoreDeviceRepository>();
        deviceRepo.Setup(r => r.GetByIdAsync(device.Id, It.IsAny<CancellationToken>())).ReturnsAsync(device);

        var storeRepo = new Mock<IStoreRepository>();
        storeRepo.Setup(r => r.GetByIdAsync(storeId, It.IsAny<CancellationToken>())).ReturnsAsync(store);

        var orderRepo = new Mock<IOrderRepository>();
        orderRepo.Setup(r => r.GetByOrderRefAsync("OH-123", It.IsAny<CancellationToken>())).ReturnsAsync(order);
        orderRepo.Setup(r => r.SaveAsync(order, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        orderRepo.Setup(r => r.AppendEventAsync(It.IsAny<OrderEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var outboxRepo = new Mock<IOutboxRepository>();
        outboxRepo.Setup(r => r.EnqueueAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var statusSync = new StatusSyncUseCase(orderRepo.Object, outboxRepo.Object, NullLogger<StatusSyncUseCase>.Instance);
        var useCase = new DeviceOrderActionUseCase(deviceRepo.Object, storeRepo.Object, orderRepo.Object, statusSync);
        return (useCase, device, orderRepo, outboxRepo);
    }

    private static StoreDevice ActiveDevice(Guid storeId, DeviceFunction functions) => new()
    {
        StoreId = storeId,
        Name = "Pass kitchen",
        Status = DeviceStatus.Active,
        Functions = functions
    };

    private static Store ActiveStore(Guid storeId) => new()
    {
        Id = storeId,
        Name = "Test Store",
        Timezone = "Africa/Johannesburg",
        State = StoreState.Active
    };
}
