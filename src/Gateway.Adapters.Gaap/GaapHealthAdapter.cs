using System.Diagnostics;
using Gateway.Application.Ports;
using Gateway.Domain.Tenancy;

namespace Gateway.Adapters.Gaap;

/// <summary>
/// GAAP has no dedicated health endpoint (ARCHITECTURE.md §2) — the cheapest
/// connectivity probe is a bounded products query.
/// </summary>
public sealed class GaapHealthAdapter(GaapApiClient client) : IPosHealthAdapter
{
    public async Task<HealthResult> PingAsync(PosConnection connection, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await client.GetProductsAsync(connection, limit: 1, skip: 0, ct);
            return new HealthResult { Healthy = true, Latency = sw.Elapsed };
        }
        catch (Exception ex)
        {
            return new HealthResult { Healthy = false, Detail = ex.Message, Latency = sw.Elapsed };
        }
    }
}
