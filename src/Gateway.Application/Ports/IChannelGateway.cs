using Gateway.Domain.Events;
using Gateway.Domain.Tenancy;

namespace Gateway.Application.Ports;

/// <summary>
/// Outbound side: signed status webhooks back to the channel (Order Harmony
/// today). One attempt per call — retry/backoff scheduling lives in
/// Gateway.Worker via Service Bus, not in this interface (ARCHITECTURE.md §6, §14).
/// </summary>
public interface IChannelGateway
{
    Task<bool> SendStatusWebhookAsync(ChannelConnection connection, OrderEvent orderEvent, CancellationToken ct);
}
