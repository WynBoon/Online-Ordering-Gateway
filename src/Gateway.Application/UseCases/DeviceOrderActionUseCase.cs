using Gateway.Application.Devices;
using Gateway.Application.Repositories;
using Gateway.Domain.Devices;
using Gateway.Domain.Enums;
using Gateway.Domain.Orders;
using Gateway.Domain.Tenancy;

namespace Gateway.Application.UseCases;

public enum DeviceOrderActionKind
{
    Preparing,
    Ready,
    Completed,
    Cancel,
    Delay
}

public sealed class DeviceOrderActionUseCase(
    IStoreDeviceRepository devices,
    IStoreRepository stores,
    IOrderRepository orders,
    StatusSyncUseCase statusSync)
{
    private static readonly OrderStatus[] Ladder =
        [OrderStatus.Accepted, OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Completed];

    public async Task<IReadOnlyList<DeviceOrderDto>> ListOpenOrdersAsync(Guid deviceId, CancellationToken ct)
    {
        var (device, _) = await RequireActiveAsync(deviceId, ct);
        var open = await orders.GetOpenOrdersByStoreAsync(device.StoreId, ct);
        return open.Select(o => Map(o, device.Functions)).ToList();
    }

    public async Task ApplyAsync(
        Guid deviceId,
        string orderRef,
        DeviceOrderActionKind action,
        CancelReason? cancelReason,
        int? delayMinutes,
        CancellationToken ct)
    {
        var (device, _) = await RequireActiveAsync(deviceId, ct);

        var order = await orders.GetByOrderRefAsync(orderRef, ct);
        if (order is null || order.StoreId != device.StoreId)
        {
            throw new InvalidOperationException($"Order {orderRef} was not found for this store.");
        }

        var detail = $"source=store_device:{device.Id}";

        switch (action)
        {
            case DeviceOrderActionKind.Preparing:
                RequireFlag(device, DeviceFunction.MarkPreparing);
                await AdvanceToAsync(device.StoreId, order, OrderStatus.Preparing, detail, ct);
                break;
            case DeviceOrderActionKind.Ready:
                RequireFlag(device, DeviceFunction.MarkReady);
                await AdvanceToAsync(device.StoreId, order, OrderStatus.Ready, detail, ct);
                break;
            case DeviceOrderActionKind.Completed:
                RequireFlag(device, DeviceFunction.MarkCompleted);
                await AdvanceToAsync(device.StoreId, order, OrderStatus.Completed, detail, ct);
                break;
            case DeviceOrderActionKind.Cancel:
                RequireFlag(device, DeviceFunction.CancelOrder);
                var applied = await statusSync.ApplyStatusAsync(
                    device.StoreId, order.OrderRef, OrderStatus.Cancelled,
                    cancelReason ?? CancelReason.MerchantRejected, ct, detail);
                if (!applied)
                {
                    throw new InvalidOperationException($"Cannot move {order.OrderRef} to {OrderStatus.Cancelled}.");
                }
                break;
            case DeviceOrderActionKind.Delay:
                RequireFlag(device, DeviceFunction.AdjustPromiseTime);
                var minutes = delayMinutes is null or < 1 or > 60 ? 5 : delayMinutes.Value;
                order.ScheduledForUtc = (order.ScheduledForUtc ?? DateTimeOffset.UtcNow) + TimeSpan.FromMinutes(minutes);
                await orders.SaveAsync(order, ct);
                break;
            default:
                throw new InvalidOperationException("Unknown action.");
        }
    }

    public static DeviceOrderDto Map(CanonicalOrder order, DeviceFunction functions)
    {
        var showContact = functions.HasFlag(DeviceFunction.ViewCustomerContact);
        var showNotes = functions.HasFlag(DeviceFunction.ViewOrderNotes);

        return new DeviceOrderDto(
            order.OrderRef,
            order.DisplayId,
            order.SourceChannel,
            order.FulfillmentType.ToString(),
            order.Status,
            order.PlacedAtUtc,
            order.ScheduledForUtc,
            order.TotalCents,
            order.Currency,
            order.Customer?.Name,
            showContact ? order.Customer?.Phone : null,
            showContact ? order.Customer?.Email : null,
            showNotes ? order.Notes : null,
            order.PosOrderId,
            order.Items.Select(i => new DeviceOrderItemDto(
                i.Name,
                i.Quantity,
                i.Modifiers.Select(m => m.Name).ToList(),
                showNotes ? i.Notes : null)).ToList());
    }

    private async Task<(StoreDevice Device, Store Store)> RequireActiveAsync(Guid deviceId, CancellationToken ct)
    {
        var device = await devices.GetByIdAsync(deviceId, ct)
            ?? throw new InvalidOperationException("Device not found.");
        if (device.Status != DeviceStatus.Active)
        {
            throw new InvalidOperationException("Device is not active.");
        }

        var store = await stores.GetByIdAsync(device.StoreId, ct)
            ?? throw new InvalidOperationException("Store not found.");
        if (store.State != StoreState.Active)
        {
            throw new InvalidOperationException($"Store is {store.State}, not accepting device actions.");
        }

        return (device, store);
    }

    private static void RequireFlag(StoreDevice device, DeviceFunction flag)
    {
        if (!device.Functions.HasFlag(flag))
        {
            throw new InvalidOperationException($"This device is not granted {flag}.");
        }
    }

    private async Task AdvanceToAsync(
        Guid storeId,
        CanonicalOrder order,
        OrderStatus to,
        string detail,
        CancellationToken ct)
    {
        var from = order.Status;
        var start = Array.IndexOf(Ladder, from);
        var end = Array.IndexOf(Ladder, to);
        if (start < 0 || end < 0 || end <= start)
        {
            return;
        }

        for (var i = start + 1; i <= end; i++)
        {
            var step = Ladder[i];
            var applied = await statusSync.ApplyStatusAsync(storeId, order.OrderRef, step, null, ct, detail);
            if (!applied)
            {
                throw new InvalidOperationException($"Cannot move {order.OrderRef} to {step}.");
            }
        }
    }
}
