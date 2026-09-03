using Gateway.Adapters.Pilot.Dtos;
using Gateway.Application.Ports;
using Gateway.Domain.Orders;
using Gateway.Domain.Tenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gateway.Adapters.Pilot;

/// <summary>
/// Maps a canonical order into Pilot's live OnlineOrderRequest — the closer
/// match to Order Harmony's own model (ARCHITECTURE.md §2). ExternalNodeId
/// carries Pilot's vendorId, ExternalLocationId carries siteId (reusing the
/// same generic PosConnection fields GAAP uses for its own node/location ids).
/// </summary>
public sealed class PilotOrderAdapter(PilotApiClient client, IOptions<PilotOptions> options, ILogger<PilotOrderAdapter> logger) : IPosOrderAdapter
{
    private readonly PilotOptions _options = options.Value;

    public async Task<PosOrderResult> CreateOrderAsync(CanonicalOrder order, PosConnection connection, CancellationToken ct)
    {
        if (!int.TryParse(connection.ExternalNodeId, out var vendorId) || !int.TryParse(connection.ExternalLocationId, out var siteId))
        {
            logger.LogError(
                "Pilot connection {ConnectionId} has non-numeric vendor/site ExternalNodeId={NodeId} ExternalLocationId={LocationId}",
                connection.Id, connection.ExternalNodeId, connection.ExternalLocationId);
            return PosOrderResult.Fail("pos_config_incomplete", "Pilot connection is missing a numeric vendorId/siteId.", retryable: false);
        }

        var nowLocal = DateTime.UtcNow;
        var timestamp = nowLocal.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var pilotOrderId = PilotIdempotency.DeriveOrderId(order.OrderRef);

        var request = new OnlineOrderRequest
        {
            VendorId = vendorId,
            SiteId = siteId,
            OrderId = pilotOrderId,
            OrderReference = order.OrderRef,
            OrderedDate = timestamp,
            CreatedDate = timestamp,
            OrderAmount = (int)order.TotalCents,
            Tip = (int)order.TipCents,
            SubBrand = order.BrandName,
            OrderStatus = new Orderstatus
            {
                StatusCode = 2,
                Description = "Pending",
                Timestamp = timestamp
            },
            Client = order.Customer is null ? null : new ClientInfo
            {
                Name = order.Customer.Name,
                Email = order.Customer.Email,
                ContactNumber = order.Customer.Phone
            },
            Delivery = new DeliveryInfo
            {
                Address = order.DeliveryAddress is null
                    ? null
                    : $"{order.DeliveryAddress.Line1} {order.DeliveryAddress.Line2}".Trim(),
                DeliveryCost = (int)order.DeliveryFeeCents,
                DeliveryMethod = order.FulfillmentType switch
                {
                    Domain.Enums.FulfillmentType.Pickup => "Collect",
                    Domain.Enums.FulfillmentType.Delivery => "Delivery",
                    _ => "Inhouse"
                }
            },
            Items = order.Items.Select(item => new OrderItem
            {
                Plu = item.ExternalProductId,
                Item = item.Name,
                Price = (int)item.UnitPriceCents,
                Quantity = item.Quantity,
                Note = item.Notes,
                Options = item.Modifiers.Select(m => new OrderItemOption
                {
                    Plu = m.ExternalModifierId,
                    Item = m.Name,
                    Price = (int)m.PriceDeltaCents
                }).ToList()
            }).ToList(),
            Payments = new OrderPayments
            {
                Status = "PAID",
                Amount = (int)order.TotalCents,
                Payment =
                [
                    new OrderPayment
                    {
                        PaymentDate = timestamp,
                        PaymentMethod = "EFT",
                        Amount = (int)order.TotalCents,
                        Reference = order.OrderRef
                    }
                ]
            },
            CallbackUrl = string.IsNullOrEmpty(_options.CallbackBaseUrl)
                ? null
                : $"{_options.CallbackBaseUrl.TrimEnd('/')}/pilot/callback/{order.OrderRef}"
        };

        logger.LogInformation(
            "Injecting Pilot order {OrderRef} plus={Plus} vendor={VendorId} site={SiteId} orderId={OrderId} amount={Amount}",
            order.OrderRef,
            string.Join(",", order.Items.Select(i => i.ExternalProductId)),
            vendorId,
            siteId,
            pilotOrderId,
            order.TotalCents);

        try
        {
            await client.CreateOnlineOrderAsync(connection, request, ct);
            logger.LogInformation("Pilot accepted order {OrderRef} as orderId {OrderId}", order.OrderRef, pilotOrderId);
            return PosOrderResult.Ok(pilotOrderId.ToString());
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            logger.LogError(ex, "Pilot rejected order {OrderRef} as unauthorized", order.OrderRef);
            return PosOrderResult.Fail("unauthorized", "Pilot token rejected.", retryable: false);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Pilot rejected order {OrderRef} ({StatusCode}): {Message}", order.OrderRef, ex.StatusCode, ex.Message);
            var retryable = ex.StatusCode is null or System.Net.HttpStatusCode.ServiceUnavailable or System.Net.HttpStatusCode.GatewayTimeout;
            return PosOrderResult.Fail("pos_failure", ex.Message, retryable);
        }
    }
}
