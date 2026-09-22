using System.Text.Json.Serialization;
using Gateway.Domain.Enums;
using Gateway.Domain.Events;
using Gateway.Domain.Orders;

namespace Gateway.Adapters.OrderHarmony.Dtos;

/// <summary>
/// Channel-side order lookup for the Harmony stand-in (and any partner that
/// wants to poll). Same five status strings as outbound webhooks. Real Order
/// Harmony can ignore these GETs — they are not in the partner spec.
/// </summary>
public sealed class ChannelOrderDto
{
    [JsonPropertyName("order_ref")]
    public required string OrderRef { get; set; }

    [JsonPropertyName("display_id")]
    public required string DisplayId { get; set; }

    [JsonPropertyName("status")]
    public required string Status { get; set; }

    [JsonPropertyName("pos_order_id")]
    public string? PosOrderId { get; set; }

    [JsonPropertyName("fulfillment_type")]
    public required string FulfillmentType { get; set; }

    [JsonPropertyName("source_channel")]
    public string? SourceChannel { get; set; }

    [JsonPropertyName("total_cents")]
    public required long TotalCents { get; set; }

    [JsonPropertyName("currency")]
    public required string Currency { get; set; }

    [JsonPropertyName("placed_at")]
    public required DateTimeOffset PlacedAt { get; set; }

    [JsonPropertyName("cancel_reason")]
    public string? CancelReason { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("customer_name")]
    public string? CustomerName { get; set; }

    [JsonPropertyName("items")]
    public required List<ChannelOrderItemDto> Items { get; set; }

    [JsonPropertyName("events")]
    public List<ChannelOrderEventDto>? Events { get; set; }
}

public sealed class ChannelOrderItemDto
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("quantity")]
    public required int Quantity { get; set; }

    [JsonPropertyName("total_price_cents")]
    public required long TotalPriceCents { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("modifiers")]
    public required List<string> Modifiers { get; set; }
}

public sealed class ChannelOrderEventDto
{
    [JsonPropertyName("status")]
    public required string Status { get; set; }

    [JsonPropertyName("event_time")]
    public required DateTimeOffset EventTime { get; set; }
}

public static class ChannelOrderMapper
{
    public static string ToStatus(OrderStatus status) => status switch
    {
        OrderStatus.Accepted => "accepted",
        OrderStatus.Preparing => "preparing",
        OrderStatus.Ready => "ready",
        OrderStatus.Completed => "completed",
        OrderStatus.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown canonical order status.")
    };

    public static string ToFulfillment(FulfillmentType fulfillment) => fulfillment switch
    {
        FulfillmentType.Delivery => "delivery",
        FulfillmentType.Pickup => "pickup",
        FulfillmentType.DineIn => "dine_in",
        _ => throw new ArgumentOutOfRangeException(nameof(fulfillment), fulfillment, "Unknown fulfillment type.")
    };

    public static ChannelOrderDto ToDto(CanonicalOrder order, IReadOnlyList<OrderEvent>? events = null)
    {
        List<ChannelOrderEventDto>? eventDtos = null;
        if (events is not null)
        {
            eventDtos = events
                .Where(e => e.Status is not null)
                .Select(e => new ChannelOrderEventDto
                {
                    Status = ToStatus(e.Status!.Value),
                    EventTime = e.EventTimeUtc
                })
                .ToList();
        }

        return new ChannelOrderDto
        {
            OrderRef = order.OrderRef,
            DisplayId = order.DisplayId,
            Status = ToStatus(order.Status),
            PosOrderId = order.PosOrderId,
            FulfillmentType = ToFulfillment(order.FulfillmentType),
            SourceChannel = order.SourceChannel,
            TotalCents = order.TotalCents,
            Currency = order.Currency,
            PlacedAt = order.PlacedAtUtc,
            CancelReason = order.CancelReason is null ? null : ToCancelReason(order.CancelReason.Value),
            Notes = order.Notes,
            CustomerName = order.Customer?.Name,
            Items = order.Items.Select(i => new ChannelOrderItemDto
            {
                Name = i.Name,
                Quantity = i.Quantity,
                TotalPriceCents = i.TotalPriceCents,
                Notes = i.Notes,
                Modifiers = i.Modifiers.Select(m => m.Name).ToList()
            }).ToList(),
            Events = eventDtos
        };
    }

    private static string ToCancelReason(CancelReason reason) => reason switch
    {
        CancelReason.OutOfStock => "out_of_stock",
        CancelReason.StoreClosed => "store_closed",
        CancelReason.PosFailure => "pos_failure",
        CancelReason.MerchantRejected => "merchant_rejected",
        CancelReason.CustomerRequest => "customer_request",
        CancelReason.Other => "other",
        _ => "other"
    };
}
