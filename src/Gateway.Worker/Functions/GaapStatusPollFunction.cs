using Gateway.Adapters.Gaap;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Gateway.Worker.Functions;

/// <summary>
/// GAAP gives no push notification, so this timer is the only way status
/// progresses for GAAP-backed orders (ARCHITECTURE.md §5). Every 5 minutes by
/// default — matches GaapOptions.StatusPollInterval.
/// </summary>
public sealed class GaapStatusPollFunction(GaapStatusSynthesizer synthesizer, ILogger<GaapStatusPollFunction> logger)
{
    [Function(nameof(GaapStatusPollFunction))]
    public async Task RunAsync([TimerTrigger("0 */5 * * * *")] TimerInfo timer, CancellationToken ct)
    {
        logger.LogInformation("Polling GAAP for pending order status at {Time}", DateTimeOffset.UtcNow);
        await synthesizer.PollPendingOrdersAsync(ct);
    }
}
