using Gateway.Application.Ports;
using Gateway.Domain.Capabilities;
using Gateway.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gateway.Adapters.Pilot;

public static class PilotServiceCollectionExtensions
{
    /// <summary>Registers the Pilot adapter under the <see cref="PosType.Pilot"/> key —
    /// resolved at runtime by <c>PosConnection.PosType</c> (ARCHITECTURE.md §3).</summary>
    public static IServiceCollection AddPilotAdapter(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PilotOptions>(configuration.GetSection(PilotOptions.SectionName));
        services.AddHttpClient<PilotTokenProvider>();
        services.AddHttpClient<PilotApiClient>();

        services.AddKeyedScoped<IPosOrderAdapter, PilotOrderAdapter>(PosType.Pilot);
        services.AddKeyedScoped<IPosMenuAdapter, PilotMenuAdapter>(PosType.Pilot);
        services.AddKeyedScoped<IPosHealthAdapter, PilotHealthAdapter>(PosType.Pilot);
        services.AddKeyedSingleton<IPosCapabilities, PilotCapabilities>(PosType.Pilot);

        return services;
    }
}
