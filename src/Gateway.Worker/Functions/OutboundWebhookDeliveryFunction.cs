using System.Text.Json;
using Gateway.Application.Ports;
using Gateway.Application.Repositories;
using Gateway.Domain.Events;
using Gateway.Infrastructure.Messaging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Gateway.Worker.Functions;

/// <summary>
/// Delivers one signed status webhook attempt per invocation. Throwing lets the
/// Service Bus trigger's own PeekLock/retry/dead-letter handling take over —
/// no custom backoff loop needed here (ARCHITECTURE.md §14). Session-enabled so
/// two events for the same store are never processed concurrently or out of
/// order.
/// </summary>
public sealed class OutboundWebhookDeliveryFunction(
    IStoreRepository storeRepository,
    IChannelGateway channelGateway,
    ILogger<OutboundWebhookDeliveryFunction> logger)
{
    [Function(nameof(OutboundWebhookDeliveryFunction))]
    public async Task RunAsync(
        [ServiceBusTrigger(OutboxDispatcher.TopicName, OutboxDispatcher.WebhookDeliverySubscription, IsSessionsEnabled = true)] string messageBody,
        CancellationToken ct)
    {
        var orderEvent = JsonSerializer.Deserialize<OrderEvent>(messageBody)
            ?? throw new InvalidOperationException("Outbound webhook message body did not deserialize to an OrderEvent.");

        var connection = await storeRepository.GetChannelConnectionAsync(orderEvent.StoreId, ct);
        if (connection is null)
        {
            logger.LogError("No ChannelConnection for store {StoreId} — dropping webhook for {OrderRef}", orderEvent.StoreId, orderEvent.OrderRef);
            return; // Nothing to retry towards — this is a data problem, not a transient failure.
        }

        var delivered = await channelGateway.SendStatusWebhookAsync(connection, orderEvent, ct);
        if (!delivered)
        {
            // Throw so the message isn't completed — Service Bus's own delivery-count
            // and lock-duration settings drive the retry cadence from here.
            throw new InvalidOperationException($"Webhook delivery failed for order {orderEvent.OrderRef}.");
        }
    }
}
