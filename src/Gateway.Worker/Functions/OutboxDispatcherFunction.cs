using Gateway.Infrastructure.Messaging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Gateway.Worker.Functions;

/// <summary>
/// The dispatcher half of the outbox pattern (ARCHITECTURE.md §14) — reads
/// rows written in the same DB transaction as the state change they describe
/// and publishes them to Service Bus. Runs every 10 seconds so an accepted
/// order's webhook goes out promptly without being on the request's own
/// critical path.
/// </summary>
public sealed class OutboxDispatcherFunction(OutboxDispatcher dispatcher, ILogger<OutboxDispatcherFunction> logger)
{
    [Function(nameof(OutboxDispatcherFunction))]
    public async Task RunAsync([TimerTrigger("*/10 * * * * *")] TimerInfo timer, CancellationToken ct)
    {
        try
        {
            await dispatcher.DispatchPendingAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Outbox dispatch failed — will retry next tick.");
        }
    }
}
