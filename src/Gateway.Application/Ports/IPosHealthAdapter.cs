using Gateway.Domain.Tenancy;

namespace Gateway.Application.Ports;

public interface IPosHealthAdapter
{
    Task<HealthResult> PingAsync(PosConnection connection, CancellationToken ct);
}
