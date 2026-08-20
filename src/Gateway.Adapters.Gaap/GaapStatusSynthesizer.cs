using Gateway.Application.Repositories;
using Gateway.Application.UseCases;
using Gateway.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Gateway.Adapters.Gaap;

/// <summary>
/// GAAP gives no feedback after injection succeeds, so the gateway owns status
/// progression itself. Implements the recommended two-state approach from
/// ARCHITECTURE.md §5: <c>Accepted</c> is emitted immediately on injection (see
/// OrderInjectionUseCase); this class is invoked on a Worker timer to confirm
/// <c>Completed</c> only once GAAP's own record shows TENDERED — never faking
/// intermediate Preparing/Ready states.
/// </summary>
public sealed class GaapStatusSynthesizer(
    IOrderRepository orderRepository,
    IStoreRepository storeRepository,
    GaapApiClient client,
    StatusSyncUseCase statusSync,
    ILogger<GaapStatusSynthesizer> logger)
{
    public async Task PollPendingOrdersAsync(CancellationToken ct)
    {
        var pending = await orderRepository.GetPendingByPosTypeAsync(PosType.Gaap, ct);

        foreach (var order in pending)
        {
            var connection = await storeRepository.GetPosConnectionAsync(order.StoreId, ct);
            if (connection is null)
            {
                continue;
            }

            try
            {
                var sales = await client.FindSaleByInvoiceNumberAsync(connection, order.OrderRef, ct);
                var sale = sales.Data.FirstOrDefault();
                if (sale is null)
                {
                    continue;
                }

                if (sale.Status == "TENDERED")
                {
                    await statusSync.ApplyStatusAsync(order.StoreId, order.OrderRef, OrderStatus.Completed, null, ct);
                }
                else if (sale.Status == "CANCELED")
                {
                    await statusSync.ApplyStatusAsync(order.StoreId, order.OrderRef, OrderStatus.Cancelled, CancelReason.PosFailure, ct);
                }
            }
            catch (Exception ex)
            {
                // One order failing to poll shouldn't stop the batch — it'll be retried
                // on the next timer tick.
                logger.LogWarning(ex, "Failed polling GAAP status for order {OrderRef}", order.OrderRef);
            }
        }
    }
}
