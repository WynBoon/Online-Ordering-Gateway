using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Gateway.Adapters.OrderHarmony.Dtos;
using Gateway.Application.Ports;
using Gateway.Domain.Enums;
using Gateway.Domain.Events;
using Gateway.Domain.Tenancy;
using Microsoft.Extensions.Logging;

namespace Gateway.Adapters.OrderHarmony;

/// <summary>
/// One signed delivery attempt per call — retry/backoff scheduling (1s, 5s, 30s,
/// 2m, 10m per doc 02 §5) lives in Gateway.Worker via Service Bus, not here
/// (ARCHITECTURE.md §6, §14).
/// </summary>
public sealed class OrderHarmonyWebhookSender(
    HttpClient httpClient,
    ISecretResolver secretResolver,
    ILogger<OrderHarmonyWebhookSender> logger) : IChannelGateway
{
    public async Task<bool> SendStatusWebhookAsync(ChannelConnection connection, OrderEvent orderEvent, CancellationToken ct)
    {
        if (orderEvent.Status is null)
        {
            return true; // Not a status event — nothing to send.
        }

        var payload = new StatusWebhookPayload
        {
            EventId = orderEvent.EventId,
            EventType = "order.status_changed",
            EventTime = orderEvent.EventTimeUtc,
            OrderRef = orderEvent.OrderRef,
            Status = orderEvent.Status.Value switch
            {
                OrderStatus.Accepted => "accepted",
                OrderStatus.Preparing => "preparing",
                OrderStatus.Ready => "ready",
                OrderStatus.Completed => "completed",
                OrderStatus.Cancelled => "cancelled",
                _ => throw new ArgumentOutOfRangeException(nameof(orderEvent))
            }
        };

        var rawBody = JsonSerializer.Serialize(payload);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var secret = await secretResolver.ResolveAsync(connection.SigningSecretRef, ct);
        var signature = OrderHarmonySignatureService.Sign(secret, timestamp, rawBody);

        var request = new HttpRequestMessage(HttpMethod.Post, connection.WebhookUrl)
        {
            Content = new StringContent(rawBody, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-OH-Signature", signature);
        request.Headers.Add("X-OH-Timestamp", timestamp.ToString());

        try
        {
            var response = await httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Order Harmony webhook for {OrderRef} returned {StatusCode}", orderEvent.OrderRef, response.StatusCode);
            }

            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Order Harmony webhook delivery failed for {OrderRef}", orderEvent.OrderRef);
            return false;
        }
    }
}
