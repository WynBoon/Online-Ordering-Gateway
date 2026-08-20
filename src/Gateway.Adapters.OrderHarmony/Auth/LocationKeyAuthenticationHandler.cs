using System.Security.Claims;
using System.Text.Encodings.Web;
using Gateway.Application.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gateway.Adapters.OrderHarmony.Auth;

public static class LocationKeyAuthenticationDefaults
{
    public const string Scheme = "LocationKey";

    /// <summary>Where the resolved Store's id ends up once authentication succeeds —
    /// controllers read this rather than re-deriving it from the header.</summary>
    public const string StoreIdClaimType = "store_id";
}

public sealed class LocationKeyAuthenticationSchemeOptions : AuthenticationSchemeOptions;

/// <summary>
/// Validates <c>Authorization: Bearer {LOCATION_KEY}</c> against a Store's
/// ChannelConnection (doc 04 §1) and resolves which Store the request is for.
/// Accepts the previous key too during a 24h rotation window, per the same doc.
/// </summary>
public sealed class LocationKeyAuthenticationHandler(
    IOptionsMonitor<LocationKeyAuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IStoreRepository storeRepository)
    : AuthenticationHandler<LocationKeyAuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader) ||
            !authHeader.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.Fail("Missing Bearer location key.");
        }

        var locationKey = authHeader.ToString()["Bearer ".Length..].Trim();
        var store = await storeRepository.GetByLocationKeyAsync(locationKey, Context.RequestAborted);
        if (store is null)
        {
            return AuthenticateResult.Fail("Unknown location key.");
        }

        var claims = new[] { new Claim(LocationKeyAuthenticationDefaults.StoreIdClaimType, store.Id.ToString()) };
        var identity = new ClaimsIdentity(claims, LocationKeyAuthenticationDefaults.Scheme);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, LocationKeyAuthenticationDefaults.Scheme));
    }
}
