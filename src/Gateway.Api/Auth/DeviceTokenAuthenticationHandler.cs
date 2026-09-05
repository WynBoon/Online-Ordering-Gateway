using System.Security.Claims;
using System.Text.Encodings.Web;
using Gateway.Application.Repositories;
using Gateway.Domain.Devices;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Gateway.Api.Auth;

/// <summary>
/// Validates <c>Authorization: Bearer {device-token}</c> against a hashed
/// Active StoreDevice. Fail closed on missing or unknown tokens. A device
/// token cannot satisfy the LocationKey scheme — different lookup.
/// </summary>
public sealed class DeviceTokenAuthenticationHandler(
    IOptionsMonitor<DeviceTokenAuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IStoreDeviceRepository devices)
    : AuthenticationHandler<DeviceTokenAuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader) ||
            !authHeader.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.Fail("Missing Bearer device token.");
        }

        var token = authHeader.ToString()["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(token))
        {
            return AuthenticateResult.Fail("Missing Bearer device token.");
        }

        var device = await devices.GetByTokenHashAsync(DeviceSecrets.Hash(token), Context.RequestAborted);
        if (device is null)
        {
            return AuthenticateResult.Fail("Unknown device token.");
        }

        var claims = new[]
        {
            new Claim(DeviceTokenAuthenticationDefaults.DeviceIdClaimType, device.Id.ToString()),
            new Claim(DeviceTokenAuthenticationDefaults.StoreIdClaimType, device.StoreId.ToString())
        };
        var identity = new ClaimsIdentity(claims, DeviceTokenAuthenticationDefaults.Scheme);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, DeviceTokenAuthenticationDefaults.Scheme));
    }
}
