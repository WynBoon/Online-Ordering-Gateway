using Gateway.Application.Ports;
using Gateway.Domain.Capabilities;
using Gateway.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gateway.Adapters.Gaap;

public static class GaapServiceCollectionExtensions
{
    /// <summary>Registers the GAAP adapter under the <see cref="PosType.Gaap"/> key —
    /// resolved at runtime by <c>PosConnection.PosType</c>, never by an if/switch in the
    /// order pipeline (ARCHITECTURE.md §3).</summary>
    public static IServiceCollection AddGaapAdapter(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GaapOptions>(configuration.GetSection(GaapOptions.SectionName));
        services.AddHttpClient<GaapApiClient>();

        services.AddKeyedScoped<IPosOrderAdapter, GaapOrderAdapter>(PosType.Gaap);
        services.AddKeyedScoped<IPosMenuAdapter, GaapMenuAdapter>(PosType.Gaap);
        services.AddKeyedScoped<IPosHealthAdapter, GaapHealthAdapter>(PosType.Gaap);
        services.AddKeyedSingleton<IPosCapabilities, GaapCapabilities>(PosType.Gaap);

        services.AddScoped<GaapStatusSynthesizer>();

        return services;
    }
}
