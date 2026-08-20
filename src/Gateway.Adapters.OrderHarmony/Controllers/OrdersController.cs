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
    IIdempotencyStore idempotencyStore) : ControllerBase
{
    private Guid CurrentStoreId => Guid.Parse(User.FindFirst(LocationKeyAuthenticationDefaults.StoreIdClaimType)!.Value);

    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrderAsync([FromBody] OrderInjectionRequest request, CancellationToken ct)
    {
        if (!Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKeyValues))
        {
            return BadRequest(new ErrorEnvelope { Code = ErrorEnvelope.Codes.InvalidPayload, Message = "Idempotency-Key header is required.", Retryable = false });
        }

        var idempotencyKey = idempotencyKeyValues.ToString();

        // Replay verbatim rather than re-processing (doc 01 §"Idempotency", ARCHITECTURE.md §6).
        var existing = await idempotencyStore.FindAsync(idempotencyKey, ct);
        if (existing is not null)
        {
            return StatusCode(existing.ResponseStatusCode, JsonSerializer.Deserialize<object>(existing.ResponseBodyJson));
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

        // Only successful/terminal responses are worth caching for replay — a request
        // that never reached the POS should be allowed to actually retry.
        if (result.Success || !result.Retryable)
        {
            await idempotencyStore.SaveAsync(new IdempotencyRecord
            {
                IdempotencyKey = idempotencyKey,
                ResponseStatusCode = statusCode,
                ResponseBodyJson = JsonSerializer.Serialize(body)
            }, ct);
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
