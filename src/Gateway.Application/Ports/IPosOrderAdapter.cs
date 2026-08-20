using Gateway.Domain.Orders;
using Gateway.Domain.Tenancy;

namespace Gateway.Application.Ports;

/// <summary>
/// Registered per <see cref="Domain.Enums.PosType"/> via keyed DI (see
/// Gateway.Api/Gateway.Worker DI wiring) — the application layer resolves the
/// right implementation by <c>PosConnection.PosType</c>, never by an if/switch
/// scattered through the order pipeline (ARCHITECTURE.md §3).
/// </summary>
public interface IPosOrderAdapter
{
    Task<PosOrderResult> CreateOrderAsync(CanonicalOrder order, PosConnection connection, CancellationToken ct);
}
