using Gateway.Domain.Menu;
using Gateway.Domain.Tenancy;

namespace Gateway.Application.Ports;

public interface IPosMenuAdapter
{
    Task<CanonicalMenu> GetMenuAsync(PosConnection connection, CancellationToken ct);
}
