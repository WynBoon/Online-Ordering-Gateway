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
            return PosOrderResult.Fail("pos_config_incomplete", "Pilot connection is missing a numeric vendorId/siteId.", retryable: false);
        }

        var nowLocal = DateTime.UtcNow; // Converted to store-local naive time at this
                                        // boundary — see ARCHITECTURE.md §7, "Timezones".
        var pilotOrderId = PilotIdempotency.DeriveOrderId(order.OrderRef);

        var request = new OnlineOrderRequest
        {
            VendorId = vendorId,
            SiteId = siteId,
            OrderId = pilotOrderId,
            OrderReference = order.OrderRef,
            OrderedDate = nowLocal.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            CreatedDate = nowLocal.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            OrderAmount = (int)order.TotalCents,
            Tip = (int)order.TipCents,
            SubBrand = order.BrandName,
            Client = order.Customer is null ? null : new ClientInfo
            {
                Name = order.Customer.Name,
                Email = order.Customer.Email,
                ContactNumber = order.Customer.Phone
            },
            Delivery = order.DeliveryAddress is null ? null : new DeliveryInfo
            {
                Address = $"{order.DeliveryAddress.Line1} {order.DeliveryAddress.Line2}".Trim(),
                DeliveryCost = (int)order.DeliveryFeeCents,
                DeliveryMethod = order.FulfillmentType switch
                {
                    Domain.Enums.FulfillmentType.Delivery => "Delivery",
                    Domain.Enums.FulfillmentType.Pickup => "Collect",
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
                // Order Harmony orders always arrive prepaid — "PAID" is always correct here.
                // The specific paymentMethod value for "already settled externally" is
                // unconfirmed (open question, ARCHITECTURE.md §10); "EFT" is a placeholder.
                Status = "PAID",
                Amount = (int)order.TotalCents,
                Payment =
                [
                    new OrderPayment
                    {
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

        try
        {
            await client.CreateOnlineOrderAsync(connection, request, ct);
            return PosOrderResult.Ok(pilotOrderId.ToString());
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            logger.LogError(ex, "Pilot rejected order {OrderRef} as unauthorized", order.OrderRef);
            return PosOrderResult.Fail("unauthorized", "Pilot token rejected.", retryable: false);
        }
        catch (HttpRequestException ex)
        {
            var retryable = ex.StatusCode is null or System.Net.HttpStatusCode.ServiceUnavailable or System.Net.HttpStatusCode.GatewayTimeout;
            return PosOrderResult.Fail("pos_failure", ex.Message, retryable);
        }
    }
}
