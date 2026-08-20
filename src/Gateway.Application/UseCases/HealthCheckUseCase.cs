using Gateway.Application.Ports;
using Gateway.Application.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Gateway.Application.UseCases;

/// <summary>Answers Order Harmony's <c>GET /health</c> — used for their connection
/// card, alerting, and before auto-pausing a location (doc 04 §2).</summary>
public sealed class HealthCheckUseCase(IStoreRepository storeRepository, IServiceProvider serviceProvider)
{
    public async Task<HealthResult> CheckAsync(Guid storeId, CancellationToken ct)
    {
        var connection = await storeRepository.GetPosConnectionAsync(storeId, ct);
        if (connection is null)
        {
            return new HealthResult { Healthy = false, Detail = "No POS connection configured." };
        }

        var adapter = serviceProvider.GetRequiredKeyedService<IPosHealthAdapter>(connection.PosType);
        return await adapter.PingAsync(connection, ct);
    }
}
