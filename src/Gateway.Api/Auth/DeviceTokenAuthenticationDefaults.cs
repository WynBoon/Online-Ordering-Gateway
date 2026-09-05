using Microsoft.AspNetCore.Authentication;

namespace Gateway.Api.Auth;

public static class DeviceTokenAuthenticationDefaults
{
    public const string Scheme = "DeviceToken";
    public const string DeviceIdClaimType = "device_id";
    public const string StoreIdClaimType = "store_id";
}

public sealed class DeviceTokenAuthenticationSchemeOptions : AuthenticationSchemeOptions;
