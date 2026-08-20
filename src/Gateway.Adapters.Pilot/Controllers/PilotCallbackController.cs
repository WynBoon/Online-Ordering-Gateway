using Gateway.Adapters.Pilot.Dtos;
using Gateway.Application.Repositories;
using Gateway.Application.UseCases;
using Gateway.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Gateway.Adapters.Pilot.Controllers;

/// <summary>
/// Receives Pilot's push to the callbackUrl we hand them per order. Payload
/// shape assumed to be the same OnlineOrderRequest with an updated orderStatus
/// — unconfirmed, open question ARCHITECTURE.md §10. Route carries our own
/// order_ref (set when we built the callbackUrl in PilotOrderAdapter), not a
/// Pilot-issued id, so this doesn't depend on Pilot's own identifiers at all.
/// </summary>
[ApiController]
[Route("pilot/callback")]
public sealed class PilotCallbackController(
    IOrderRepository orderRepository,
    StatusSyncUseCase statusSync,
    ILogger<PilotCallbackController> logger) : ControllerBase
{
    [HttpPost("{orderRef}")]
    public async Task<IActionResult> ReceiveAsync(string orderRef, [FromBody] OnlineOrderRequest payload, CancellationToken ct)
    {
        var order = await orderRepository.GetByOrderRefAsync(orderRef, ct);
        if (order is null)
        {
            return NotFound();
        }

        var statusCode = payload.OrderStatus?.StatusCode;
        if (statusCode is null || !PilotStatusCodeMapping.TryMap(statusCode.Value, out var status))
        {
            logger.LogWarning("Unmapped Pilot statusCode {StatusCode} for order {OrderRef}", statusCode, orderRef);
            return Ok(); // 2xx per doc 02 §5 — malformed/unknown status isn't Pilot's fault to retry forever.
        }

        CancelReason? cancelReason = status == OrderStatus.Cancelled ? CancelReason.PosFailure : null;
        await statusSync.ApplyStatusAsync(order.StoreId, orderRef, status, cancelReason, ct);
        return Ok();
    }
}
