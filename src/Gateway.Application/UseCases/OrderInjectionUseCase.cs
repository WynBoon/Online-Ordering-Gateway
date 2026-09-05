using System.Diagnostics;
using System.Text.Json;
using Gateway.Application.Ports;
using Gateway.Application.Repositories;
using Gateway.Domain.Enums;
using Gateway.Domain.Events;
using Gateway.Domain.Orders;
using Gateway.Domain.Outbox;
using Gateway.Domain.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Gateway.Application.UseCases;

/// <summary>
/// The core inbound flow: Order Harmony's <c>POST /orders</c> lands here (via the
/// Adapters.OrderHarmony controller) after auth/idempotency have already been
/// handled at the HTTP layer. This use case owns Store-state gating, adapter
/// resolution by <see cref="PosType"/>, and the resulting webhook enqueue —
/// see ARCHITECTURE.md §3 and §7 ("Store lifecycle").
/// </summary>
public sealed class OrderInjectionUseCase(
    IStoreRepository storeRepository,
    IOrderRepository orderRepository,
    IOutboxRepository outboxRepository,
    IServiceProvider serviceProvider,
    ILogger<OrderInjectionUseCase> logger)
{
    public async Task<OrderInjectionResult> ExecuteAsync(CanonicalOrder order, CancellationToken ct)
    {
        var store = await storeRepository.GetByIdAsync(order.StoreId, ct);
        if (store is null)
        {
            return OrderInjectionResult.Fail("unknown_location", "Store not found.", retryable: false);
        }

        // A document doesn't get to flow through the gateway just because it arrived —
        // the store has to be in a state that allows it (ARCHITECTURE.md §7).
        if (store.State != StoreState.Active)
        {
            logger.LogWarning("Rejected order {OrderRef} for store {StoreId} in state {State}", order.OrderRef, store.Id, store.State);
            return OrderInjectionResult.Fail("store_not_active", $"Store is {store.State}, not accepting orders.", retryable: false);
        }

        var connection = await storeRepository.GetPosConnectionAsync(store.Id, ct);
        if (connection is null)
        {
            return OrderInjectionResult.Fail("unknown_location", "Store has no POS connection configured.", retryable: false);
        }

        var adapter = serviceProvider.GetRequiredKeyedService<IPosOrderAdapter>(connection.PosType);
        var sw = Stopwatch.StartNew();
        var result = await adapter.CreateOrderAsync(order, connection, ct);
        sw.Stop();

        if (!result.Success)
        {
            logger.LogError(
                "Order {OrderRef} injection failed for store {StoreId} pos={PosType}: {ErrorCode} {ErrorMessage}",
                order.OrderRef, store.Id, connection.PosType, result.ErrorCode, result.ErrorMessage);

            var failed = new OrderEvent
            {
                StoreId = store.Id,
                OrderRef = order.OrderRef,
                EventId = Guid.NewGuid().ToString(),
                EventType = "order.injection_failed",
                Outcome = result.ErrorCode,
                Detail = PosOrderResult.Truncate(result.Detail ?? result.ErrorMessage),
                DurationMs = sw.ElapsedMilliseconds
            };
            await orderRepository.AppendEventAsync(failed, ct);
            await outboxRepository.EnqueueAsync(new OutboxMessage
            {
                SessionId = store.Id.ToString(),
                MessageType = "order.injection_failed",
                PayloadJson = JsonSerializer.Serialize(failed)
            }, ct);

            return OrderInjectionResult.Fail(result.ErrorCode ?? "pos_failure", result.ErrorMessage ?? "Injection failed.", result.Retryable);
        }

        order.PosOrderId = result.PosOrderId;
        order.Status = OrderStatus.Accepted;
        await orderRepository.SaveAsync(order, ct);

        var accepted = new OrderEvent
        {
            StoreId = store.Id,
            OrderRef = order.OrderRef,
            EventId = Guid.NewGuid().ToString(),
            EventType = "order.status_changed",
            Status = OrderStatus.Accepted,
            Outcome = "success",
            Detail = PosOrderResult.Truncate(result.Detail ?? $"posOrderId={result.PosOrderId}"),
            DurationMs = sw.ElapsedMilliseconds
        };
        await orderRepository.AppendEventAsync(accepted, ct);

        // Order Harmony expects the status webhook as a separate step from the
        // synchronous pos_order_id response (doc 02, lifecycle steps 4-5) — enqueued
        // via the outbox, not sent inline, so a slow/failing webhook can never affect
        // the synchronous response Order Harmony is waiting on (ARCHITECTURE.md §14).
        await outboxRepository.EnqueueAsync(new OutboxMessage
        {
            SessionId = store.Id.ToString(),
            MessageType = "order.status_changed",
            PayloadJson = JsonSerializer.Serialize(accepted)
        }, ct);

        return OrderInjectionResult.Ok(result.PosOrderId!);
    }
}
