using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Gateway.Domain.Events;
using Gateway.Infrastructure.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Gateway.Portal.Services;

/// <summary>
/// The live half of the command centre (UI-ARCHITECTURE.md, ARCHITECTURE.md §12).
/// Subscribes to the portal-live-feed subscription on the shared order-events
/// topic and fans out in-process to connected Blazor circuits via a plain C#
/// event — no Azure SignalR Service backplane needed while the portal runs as
/// a single instance. Revisit if the portal is ever scaled to multiple
/// instances (UI-ARCHITECTURE.md, "Decided").
/// </summary>
public sealed class LiveOrderFeedService(ILogger<LiveOrderFeedService> logger, ServiceBusClient? serviceBusClient = null) : IHostedService
{
    private ServiceBusProcessor? _processor;

    public event Func<OrderEvent, Task>? OrderEventReceived;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (serviceBusClient is null)
        {
            logger.LogInformation("Service Bus is not configured — live ticker is idle. Set ConnectionStrings:ServiceBus to enable it.");
            return;
        }

        _processor = serviceBusClient.CreateProcessor(OutboxDispatcher.TopicName, OutboxDispatcher.PortalLiveFeedSubscription);
        _processor.ProcessMessageAsync += OnMessageAsync;
        _processor.ProcessErrorAsync += args =>
        {
            logger.LogWarning(args.Exception, "Live order feed processor error");
            return Task.CompletedTask;
        };
        await _processor.StartProcessingAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor is not null)
        {
            await _processor.StopProcessingAsync(cancellationToken);
            await _processor.DisposeAsync();
        }
    }

    private async Task OnMessageAsync(ProcessMessageEventArgs args)
    {
        try
        {
            var orderEvent = JsonSerializer.Deserialize<OrderEvent>(args.Message.Body.ToString());
            if (orderEvent is not null && OrderEventReceived is not null)
            {
                await OrderEventReceived.Invoke(orderEvent);
            }

            await args.CompleteMessageAsync(args.Message);
        }
        catch (Exception ex)
        {
            // Best-effort feed — a malformed message shouldn't jam the live view.
            logger.LogWarning(ex, "Failed to process live feed message");
            await args.CompleteMessageAsync(args.Message);
        }
    }
}
