using Gateway.Domain.Tenancy;

namespace Gateway.Application.Ports;

/// <summary>
/// Phase 2 (ARCHITECTURE.md §11) — stock/86 propagation. Only wired up for a
/// POS whose <see cref="Domain.Capabilities.IPosCapabilities.SupportsInboundStockWrite"/>
/// is true; neither GAAP nor Pilot has confirmed this is possible yet.
/// </summary>
public interface IPosStockAdapter
{
    Task SetItemAvailabilityAsync(PosConnection connection, string externalProductId, bool available, CancellationToken ct);
}
