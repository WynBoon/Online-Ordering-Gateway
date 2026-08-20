using Gateway.Adapters.OrderHarmony.Auth;
using Gateway.Application.Ports;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace Gateway.Adapters.OrderHarmony;

public static class OrderHarmonyServiceCollectionExtensions
{
    public static IServiceCollection AddOrderHarmonyChannel(this IServiceCollection services)
    {
        services.AddHttpClient<IChannelGateway, OrderHarmonyWebhookSender>();

        services
            .AddAuthentication(LocationKeyAuthenticationDefaults.Scheme)
            .AddScheme<LocationKeyAuthenticationSchemeOptions, LocationKeyAuthenticationHandler>(
                LocationKeyAuthenticationDefaults.Scheme, _ => { });

        services.AddAuthorization();

        return services;
    }
}
