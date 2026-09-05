using Gateway.Api.Auth;
using Gateway.Application.UseCases;
using Gateway.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gateway.Api.Controllers;

[ApiController]
[Route("device")]
public sealed class DeviceController(
    DeviceEnrollmentUseCase enrollment,
    DeviceOrderActionUseCase actions) : ControllerBase
{
    private Guid CurrentDeviceId =>
        Guid.Parse(User.FindFirst(DeviceTokenAuthenticationDefaults.DeviceIdClaimType)!.Value);

    [AllowAnonymous]
    [HttpPost("enroll")]
    public async Task<IActionResult> EnrollAsync([FromBody] EnrollRequest body, CancellationToken ct)
    {
        try
        {
            var claimed = await enrollment.ClaimAsync(
                body.EnrollmentCode ?? "",
                body.DeviceName,
                body.HardwareFingerprint,
                ct);
            return Ok(claimed);
        }
        catch (InvalidOperationException ex)
        {
            return Error(ex.Message);
        }
    }

    [Authorize(AuthenticationSchemes = DeviceTokenAuthenticationDefaults.Scheme)]
    [HttpPost("heartbeat")]
    public async Task<IActionResult> HeartbeatAsync(CancellationToken ct)
    {
        try
        {
            await enrollment.HeartbeatAsync(CurrentDeviceId, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Error(ex.Message);
        }
    }

    [Authorize(AuthenticationSchemes = DeviceTokenAuthenticationDefaults.Scheme)]
    [HttpGet("me")]
    public async Task<IActionResult> MeAsync(CancellationToken ct)
    {
        try
        {
            return Ok(await enrollment.ToSessionAsync(CurrentDeviceId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return Error(ex.Message);
        }
    }

    [Authorize(AuthenticationSchemes = DeviceTokenAuthenticationDefaults.Scheme)]
    [HttpGet("orders")]
    public async Task<IActionResult> OrdersAsync(CancellationToken ct)
    {
        try
        {
            return Ok(await actions.ListOpenOrdersAsync(CurrentDeviceId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return Error(ex.Message);
        }
    }

    [Authorize(AuthenticationSchemes = DeviceTokenAuthenticationDefaults.Scheme)]
    [HttpPost("orders/{orderRef}/actions")]
    public async Task<IActionResult> ActionAsync(string orderRef, [FromBody] DeviceActionRequest body, CancellationToken ct)
    {
        if (!TryParseAction(body.Action, out var kind))
        {
            return Error("Unknown action.");
        }

        CancelReason? cancelReason = null;
        if (!string.IsNullOrWhiteSpace(body.CancelReason))
        {
            if (!Enum.TryParse<CancelReason>(body.CancelReason, ignoreCase: true, out var parsed))
            {
                return Error("Unknown cancel reason.");
            }

            cancelReason = parsed;
        }

        try
        {
            await actions.ApplyAsync(CurrentDeviceId, orderRef, kind, cancelReason, body.DelayMinutes, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Error(ex.Message);
        }
    }

    private BadRequestObjectResult Error(string message) =>
        BadRequest(new { error = message });

    private static bool TryParseAction(string? action, out DeviceOrderActionKind kind)
    {
        kind = default;
        if (string.IsNullOrWhiteSpace(action))
        {
            return false;
        }

        return Enum.TryParse(action, ignoreCase: true, out kind);
    }

    public sealed record EnrollRequest(string? EnrollmentCode, string? DeviceName, string? HardwareFingerprint);

    public sealed record DeviceActionRequest(string? Action, string? CancelReason, int? DelayMinutes);
}
