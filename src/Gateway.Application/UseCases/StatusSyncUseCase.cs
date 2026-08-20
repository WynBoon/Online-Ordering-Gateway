using System.Text.Json;
using Gateway.Application.Repositories;
using Gateway.Domain.Enums;
using Gateway.Domain.Events;
using Gateway.Domain.Orders;
using Gateway.Domain.Outbox;
using Microsoft.Extensions.Logging;

namespace Gateway.Application.UseCases;

/// <summary>
/// Feeds from two very different sources into one canonical vocabulary: Pilot's
/// callback receiver calls this directly with a real status; the GAAP status
/// synthesizer (invoked on a Worker timer) calls this with a synthesized one.
/// Neither the order pipeline nor Order Harmony can tell the difference —
/// that's the point of the capability-flag design (ARCHITECTURE.md §3, §5).
/// </summary>
public sealed class StatusSyncUseCase(
    IOrderRepository orderRepository,
    IOutboxRepository outboxRepository,
    ILogger<StatusSyncUseCase> logger)
{
    public async Task<bool> ApplyStatusAsync(
        Guid storeId,
        string orderRef,
        OrderStatus newStatus,
        CancelReason? cancelReason,
        CancellationToken ct)
    {
        var order = await orderRepository.GetByOrderRefAsync(orderRef, ct);
        if (order is null)
        {
            logger.LogWarning("Status update for unknown order {OrderRef}", orderRef);
            return false;
        }

        if (!OrderStatusTransition.CanTransition(order.Status, newStatus))
        {
            // Never regress a terminal status — ordering isn't guaranteed on delivery,
            // so a stale retried event must not undo a later one (ARCHITECTURE.md §6).
            logger.LogInformation(
                "Ignoring illegal transition {From} -> {To} for order {OrderRef}", order.Status, newStatus, orderRef);
            return false;
        }

        if (order.Status == newStatus)
        {
            // Idempotent replay — nothing changed, nothing to re-announce.
            return true;
        }

        order.Status = newStatus;
        order.CancelReason = cancelReason;
        await orderRepository.SaveAsync(order, ct);

        var statusEvent = new OrderEvent
        {
            StoreId = storeId,
            OrderRef = orderRef,
            EventId = Guid.NewGuid().ToString(),
            EventType = "order.status_changed",
            Status = newStatus,
            Outcome = "success"
        };
        await orderRepository.AppendEventAsync(statusEvent, ct);

        await outboxRepository.EnqueueAsync(new OutboxMessage
        {
            SessionId = storeId.ToString(),
            MessageType = "order.status_changed",
            PayloadJson = JsonSerializer.Serialize(statusEvent)
        }, ct);

        return true;
    }
}
