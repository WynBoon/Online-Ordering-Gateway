using Gateway.Application.Repositories;
using Gateway.Application.UseCases;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Gateway.Worker.Functions;

/// <summary>
/// Order Harmony re-pulls a store's menu itself on a schedule and on demand
/// (doc 03 §1) — this timer's job is only to catch drift <em>we</em> need to
/// know about (e.g. a product disappearing, which would otherwise only surface
/// as an unknown_plu failure at order time). Runs hourly.
///
/// TODO: this only re-fetches — it doesn't yet diff against the previous pull
/// or fire a <c>menu.changed</c> webhook when something actually changed. That
/// diffing logic is Phase 4 scope (ARCHITECTURE.md §11) — deliberately not
/// built here as a first pass rather than guessed at.
/// </summary>
public sealed class ScheduledMenuRepullFunction(
    IStoreRepository storeRepository,
    MenuSyncUseCase menuSync,
    ILogger<ScheduledMenuRepullFunction> logger)
{
    [Function(nameof(ScheduledMenuRepullFunction))]
    public async Task RunAsync([TimerTrigger("0 0 * * * *")] TimerInfo timer, CancellationToken ct)
    {
        var stores = await storeRepository.GetActiveStoresAsync(ct);
        foreach (var store in stores)
        {
            try
            {
                await menuSync.GetMenuAsync(store.Id, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Scheduled menu re-pull failed for store {StoreId}", store.Id);
            }
        }
    }
}
