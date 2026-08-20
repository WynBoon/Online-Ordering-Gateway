using System.Diagnostics;
using Gateway.Application.Ports;
using Gateway.Domain.Tenancy;

namespace Gateway.Adapters.Pilot;

public sealed class PilotHealthAdapter(PilotApiClient client) : IPosHealthAdapter
{
    public async Task<HealthResult> PingAsync(PosConnection connection, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var healthy = await client.CheckHealthAsync(connection, ct);
            return new HealthResult { Healthy = healthy, Latency = sw.Elapsed };
        }
        catch (Exception ex)
        {
            return new HealthResult { Healthy = false, Detail = ex.Message, Latency = sw.Elapsed };
        }
    }
}
