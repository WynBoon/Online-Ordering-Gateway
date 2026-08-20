using Gateway.Application.Ports;
using Gateway.Application.Repositories;
using Gateway.Domain.Menu;
using Microsoft.Extensions.DependencyInjection;

namespace Gateway.Application.UseCases;

/// <summary>Answers Order Harmony's <c>GET /menu</c> — reshapes the POS catalogue on
/// every call, no persisted mapping in between (ARCHITECTURE.md §7).</summary>
public sealed class MenuSyncUseCase(IStoreRepository storeRepository, IServiceProvider serviceProvider)
{
    public async Task<CanonicalMenu?> GetMenuAsync(Guid storeId, CancellationToken ct)
    {
        var connection = await storeRepository.GetPosConnectionAsync(storeId, ct);
        if (connection is null)
        {
            return null;
        }

        var adapter = serviceProvider.GetRequiredKeyedService<IPosMenuAdapter>(connection.PosType);
        return await adapter.GetMenuAsync(connection, ct);
    }
}
