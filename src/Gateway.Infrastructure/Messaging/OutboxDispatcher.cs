using Azure.Messaging.ServiceBus;
using Gateway.Application.Repositories;

namespace Gateway.Infrastructure.Messaging;

/// <summary>
/// The dispatcher half of the outbox pattern (ARCHITECTURE.md §14): reads rows
/// written in the same DB transaction as the state change they describe, and
/// publishes them to Service Bus. Invoked on a Worker timer, not inline with
/// the request that created the outbox row — that's the whole point of the
/// pattern.
///
/// Publishes to a <b>topic</b>, not a queue, because two independent
/// consumers need every event: the Worker's webhook-delivery subscription
/// (session-enabled, must not lose or reorder a message — ARCHITECTURE.md
/// §14) and the Portal's live-feed subscription (best-effort, just for the
/// command centre's live ticker — ARCHITECTURE.md §12, UI-ARCHITECTURE.md).
/// </summary>
public sealed class OutboxDispatcher(IOutboxRepository outbox, ServiceBusClient serviceBusClient)
{
    public const string TopicName = "order-events";
    public const string WebhookDeliverySubscription = "webhook-delivery";
    public const string PortalLiveFeedSubscription = "portal-live-feed";

    public async Task DispatchPendingAsync(CancellationToken ct)
    {
        var pending = await outbox.GetUndispatchedAsync(batchSize: 100, ct);
        if (pending.Count == 0)
        {
            return;
        }

        await using var sender = serviceBusClient.CreateSender(TopicName);
        foreach (var message in pending)
        {
            var sbMessage = new ServiceBusMessage(message.PayloadJson)
            {
                // Session-enabled queue keyed on StoreId — guarantees per-store ordering
                // and prevents concurrent processing of two messages for the same store
                // (ARCHITECTURE.md §14).
                SessionId = message.SessionId,
                Subject = message.MessageType,
                MessageId = message.Id.ToString()
            };
            await sender.SendMessageAsync(sbMessage, ct);
            await outbox.MarkDispatchedAsync(message.Id, ct);
        }
    }
}
