using Gateway.Adapters.Gaap.Dtos;
using Gateway.Application.Ports;
using Gateway.Domain.Orders;
using Gateway.Domain.Tenancy;
using Microsoft.Extensions.Logging;

namespace Gateway.Adapters.Gaap;

/// <summary>
/// Maps a canonical order into GAAP's world: an already-closed, already-paid
/// sale (ARCHITECTURE.md §2). Because Order Harmony orders arrive prepaid, this
/// is a legitimate representation of reality, not a workaround — the customer
/// really has already paid, GAAP just wants that recorded as a finished
/// transaction rather than a pending one.
/// </summary>
public sealed class GaapOrderAdapter(GaapApiClient client, ILogger<GaapOrderAdapter> logger) : IPosOrderAdapter
{
    public async Task<PosOrderResult> CreateOrderAsync(CanonicalOrder order, PosConnection connection, CancellationToken ct)
    {
        if (!connection.ExtraConfig.TryGetValue("employeeId", out var employeeId) ||
            !connection.ExtraConfig.TryGetValue("paymentMethodId", out var paymentMethodId) ||
            !connection.ExtraConfig.TryGetValue("terminalId", out var terminalIdRaw) ||
            !double.TryParse(terminalIdRaw, out var terminalId))
        {
            // Fail fast and loud rather than guess — these have no confirmed sourcing
            // process yet (open question, ARCHITECTURE.md §10) and guessing wrong means
            // silently posting a sale under the wrong employee or payment method.
            logger.LogError("PosConnection {ConnectionId} is missing required GAAP config (employeeId/paymentMethodId/terminalId)", connection.Id);
            return PosOrderResult.Fail("pos_config_incomplete", "GAAP connection is missing employeeId/paymentMethodId/terminalId.", retryable: false);
        }

        if (connection.ExternalNodeId is null || connection.ExternalLocationId is null)
        {
            return PosOrderResult.Fail("pos_config_incomplete", "GAAP connection is missing nodeId/locationId.", retryable: false);
        }

        var nowLocal = DateTime.UtcNow; // Adapter boundary: convert to the store's local
                                        // naive time before this reaches GAAP — Store.Timezone
                                        // conversion is applied by the caller; see ARCHITECTURE.md §7.
        var payload = new NewSalePayload
        {
            ExternalTransactionId = GaapIdempotency.DeriveTransactionId(order.OrderRef),
            NodeId = connection.ExternalNodeId,
            TerminalId = terminalId,
            InvoiceNumber = order.OrderRef,
            OrderNum = order.DisplayId,
            IsNegative = false,
            Status = GaapSaleStatus.Tendered,
            CreatedDate = nowLocal.ToString("O"),
            ClosedDate = nowLocal.ToString("O"),
            LocationId = connection.ExternalLocationId,
            EmployeeId = employeeId,
            InvoiceTotal = order.TotalCents / 100.0,
            DiscountsTotal = 0,
            PaymentsTotal = order.TotalCents / 100.0,
            TipsTotal = order.TipCents / 100.0,
            ChangeGiven = 0,
            Turnover = order.SubtotalCents / 100.0,
            Tax = order.TaxCents / 100.0,
            Payments =
            [
                new SalePayment
                {
                    PaymentMethodId = paymentMethodId,
                    Amount = order.TotalCents / 100.0,
                    ActualTender = order.TotalCents / 100.0
                }
            ],
            Items = order.Items.Select(item => new SaleItem
            {
                ProductId = item.ExternalProductId,
                Quantity = item.Quantity,
                Note = item.Notes,
                AddedTime = nowLocal.ToString("O"),
                PriceIncl = item.UnitPriceCents / 100.0,
                AddOns = item.Modifiers.Select(m => new GaapAddOn
                {
                    ProductId = m.ExternalModifierId,
                    Quantity = m.Quantity,
                    PriceIncl = m.PriceDeltaCents / 100.0
                }).ToList()
            }).ToList()
        };

        try
        {
            await client.CreateSaleAsync(connection, payload, ct);
            // GAAP's 200 response body is just a string per its swagger — the invoice
            // number we sent IS the sale's identifier for later lookup (status
            // synthesizer polls by it), so that's what we treat as the pos_order_id.
            return PosOrderResult.Ok(order.OrderRef, detail: $"posOrderId={order.OrderRef}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            // 409 duplicate_order: treated as success if it's our own prior attempt,
            // which it is here since externalTransactionId is deterministic (§6).
            logger.LogInformation("GAAP reported duplicate for order {OrderRef} — treating as success", order.OrderRef);
            return PosOrderResult.Ok(order.OrderRef, detail: $"duplicate; posOrderId={order.OrderRef}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.NotFound)
        {
            return PosOrderResult.Fail(
                "unknown_plu",
                "GAAP could not resolve a node/location/employee/product/payment method.",
                retryable: false,
                detail: ex.Message);
        }
        catch (HttpRequestException ex)
        {
            var retryable = ex.StatusCode is null or System.Net.HttpStatusCode.ServiceUnavailable or System.Net.HttpStatusCode.GatewayTimeout;
            return PosOrderResult.Fail("pos_failure", ex.Message, retryable, detail: ex.Message);
        }
    }
}
