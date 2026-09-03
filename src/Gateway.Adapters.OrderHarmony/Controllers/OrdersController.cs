using System.Text.Json;
using Gateway.Adapters.OrderHarmony.Auth;
using Gateway.Adapters.OrderHarmony.Dtos;
using Gateway.Application.Repositories;
using Gateway.Application.UseCases;
using Gateway.Domain.Enums;
using Gateway.Domain.Idempotency;
using Gateway.Domain.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Gateway.Adapters.OrderHarmony.Controllers;

/// <summary>
/// Implements doc 01/03/04's minimum viable scope for a POS partner: order
/// injection, menu pull, health probe. Auth is the LocationKey scheme
/// (Bearer {LOCATION_KEY}) resolved per doc 04 §1.
/// </summary>
[ApiController]
[Authorize(AuthenticationSchemes = LocationKeyAuthenticationDefaults.Scheme)]
public sealed class OrdersController(
    OrderInjectionUseCase orderInjection,
    MenuSyncUseCase menuSync,
    HealthCheckUseCase healthCheck,
    IIdempotencyStore idempotencyStore,
    ILogger<OrdersController> logger) : ControllerBase
{
    private Guid CurrentStoreId => Guid.Parse(User.FindFirst(LocationKeyAuthenticationDefaults.StoreIdClaimType)!.Value);

    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrderAsync([FromBody] OrderInjectionRequest request, CancellationToken ct)
    {
        if (!Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKeyValues))
        {
            logger.LogWarning("POST /orders missing Idempotency-Key header for {OrderRef}", request.OrderRef);
            return BadRequest(new ErrorEnvelope { Code = ErrorEnvelope.Codes.InvalidPayload, Message = "Idempotency-Key header is required.", Retryable = false });
        }

        var idempotencyKey = idempotencyKeyValues.ToString();
        logger.LogInformation(
            "POST /orders {OrderRef} store={StoreId} idempotency={IdempotencyKey} items={ItemCount}",
            request.OrderRef, CurrentStoreId, idempotencyKey, request.Items.Count);

        // Replay verbatim rather than re-processing (doc 01 §"Idempotency", ARCHITECTURE.md §6).
        // Only successful 2xx are replayed. A cached Pilot 400 would otherwise hide every
        // retry (including after we fix the adapter) — failures are allowed to re-run.
        var existing = await idempotencyStore.FindAsync(idempotencyKey, ct);
        if (existing is not null && existing.ResponseStatusCode is >= 200 and < 300)
        {
            logger.LogInformation(
                "Replaying cached {StatusCode} for {OrderRef} idempotency={IdempotencyKey}",
                existing.ResponseStatusCode, request.OrderRef, idempotencyKey);
            return StatusCode(existing.ResponseStatusCode, JsonSerializer.Deserialize<object>(existing.ResponseBodyJson));
        }

        if (existing is not null)
        {
            logger.LogWarning(
                "Ignoring cached failed {StatusCode} for {OrderRef} idempotency={IdempotencyKey}: {Body}",
                existing.ResponseStatusCode, request.OrderRef, idempotencyKey, existing.ResponseBodyJson);
            await idempotencyStore.RemoveAsync(idempotencyKey, ct);
        }

        var order = MapToCanonical(request, CurrentStoreId);
        var result = await orderInjection.ExecuteAsync(order, ct);

        IActionResult response;
        int statusCode;
        object body;

        if (result.Success)
        {
            statusCode = StatusCodes.Status201Created;
            body = new OrderInjectionResponse { PosOrderId = result.PosOrderId! };
            response = StatusCode(statusCode, body);
        }
        else
        {
            statusCode = result.ErrorCode switch
            {
                "unknown_location" or "unknown_plu" => StatusCodes.Status404NotFound,
                "store_not_active" or "store_closed" => StatusCodes.Status409Conflict,
                "modifier_rule_violation" => StatusCodes.Status422UnprocessableEntity,
                "pos_config_incomplete" or "invalid_payload" => StatusCodes.Status400BadRequest,
                _ => result.Retryable ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status400BadRequest
            };
            body = new ErrorEnvelope { Code = result.ErrorCode!, Message = result.ErrorMessage!, Retryable = result.Retryable };
            response = StatusCode(statusCode, body);
        }

        // Cache successful injections only (ARCHITECTURE.md §6: replay 200/201).
        if (result.Success)
        {
            await idempotencyStore.SaveAsync(new IdempotencyRecord
            {
                IdempotencyKey = idempotencyKey,
                ResponseStatusCode = statusCode,
                ResponseBodyJson = JsonSerializer.Serialize(body)
            }, ct);
        }
        else
        {
            logger.LogError(
                "POST /orders {OrderRef} failed {StatusCode} {ErrorCode}: {Message}",
                request.OrderRef, statusCode, result.ErrorCode, result.ErrorMessage);
        }

        return response;
    }

    [HttpGet("menu")]
    public async Task<IActionResult> GetMenuAsync(CancellationToken ct)
    {
        var menu = await menuSync.GetMenuAsync(CurrentStoreId, ct);
        if (menu is null)
        {
            return NotFound(new ErrorEnvelope { Code = ErrorEnvelope.Codes.UnknownLocation, Message = "No POS connection configured for this store.", Retryable = false });
        }

        var dto = new MenuResponseDto
        {
            Categories = menu.Categories.Select(c => new MenuCategoryDto
            {
                ExternalId = c.ExternalId,
                Name = c.Name,
                Products = c.Products.Select(p => new MenuProductDto
                {
                    ExternalId = p.ExternalId,
                    Name = p.Name,
                    Description = p.Description,
                    PriceCents = p.PriceCents,
                    TaxRateBp = p.TaxRateBp,
                    ModifierGroups = p.ModifierGroups.Select(g => new ModifierGroupDto
                    {
                        ExternalId = g.ExternalId,
                        Name = g.Name,
                        MinSelect = g.MinSelect,
                        MaxSelect = g.MaxSelect,
                        Modifiers = g.Modifiers.Select(m => new MenuModifierDto
                        {
                            ExternalId = m.ExternalId,
                            Name = m.Name,
                            PriceDeltaCents = m.PriceDeltaCents
                        }).ToList()
                    }).ToList()
                }).ToList()
            }).ToList()
        };

        return Ok(dto);
    }

    [HttpGet("health")]
    public async Task<IActionResult> GetHealthAsync(CancellationToken ct)
    {
        var result = await healthCheck.CheckAsync(CurrentStoreId, ct);
        return result.Healthy ? Ok(new { status = "ok" }) : StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "degraded", detail = result.Detail });
    }

    private static CanonicalOrder MapToCanonical(OrderInjectionRequest request, Guid storeId) => new()
    {
        OrderRef = request.OrderRef,
        DisplayId = request.DisplayId,
        SourceChannel = request.SourceChannel,
        BrandName = request.BrandName,
        StoreId = storeId,
        FulfillmentType = request.FulfillmentType switch
        {
            "delivery" => FulfillmentType.Delivery,
            "pickup" => FulfillmentType.Pickup,
            "dine_in" => FulfillmentType.DineIn,
            _ => throw new ArgumentOutOfRangeException(nameof(request), $"Unknown fulfillment_type '{request.FulfillmentType}'.")
        },
        PlacedAtUtc = request.PlacedAt,
        ScheduledForUtc = request.ScheduledFor,
        Customer = request.Customer is null ? null : new CustomerInfo
        {
            Name = request.Customer.Name,
            Phone = request.Customer.Phone,
            Email = request.Customer.Email
        },
        DeliveryAddress = request.DeliveryAddress is null ? null : new DeliveryAddress
        {
            Line1 = request.DeliveryAddress.Line1,
            Line2 = request.DeliveryAddress.Line2,
            City = request.DeliveryAddress.City,
            PostalCode = request.DeliveryAddress.PostalCode,
            Notes = request.DeliveryAddress.Notes
        },
        Items = request.Items.Select(i => new CanonicalOrderItem
        {
            ExternalProductId = i.ExternalProductId,
            Name = i.Name,
            Quantity = i.Quantity,
            UnitPriceCents = i.UnitPriceCents,
            TotalPriceCents = i.TotalPriceCents,
            Notes = i.Notes,
            Modifiers = i.Modifiers.Select(m => new CanonicalModifier
            {
                ExternalModifierId = m.ExternalModifierId,
                GroupExternalId = m.GroupExternalId,
                Name = m.Name,
                Quantity = m.Quantity,
                PriceDeltaCents = m.PriceDeltaCents
            }).ToList()
        }).ToList(),
        SubtotalCents = request.SubtotalCents,
        TaxCents = request.TaxCents,
        DeliveryFeeCents = request.DeliveryFeeCents,
        TipCents = request.TipCents,
        TotalCents = request.TotalCents,
        Currency = request.Currency,
        Prepaid = request.Payment.Prepaid,
        Notes = request.Notes
    };
}
