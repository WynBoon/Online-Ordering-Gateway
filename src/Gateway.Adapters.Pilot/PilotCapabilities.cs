using Gateway.Domain.Capabilities;

namespace Gateway.Adapters.Pilot;

public sealed class PilotCapabilities : IPosCapabilities
{
    /// <summary>Pending Pilot's confirmation of the statusCode table and callback payload
    /// shape (open question, ARCHITECTURE.md §10) — true in principle, not yet certified.</summary>
    public bool SupportsRealtimeOrderStatus => true;
    public bool RequiresPrepaidClosedSale => false;
    public bool SupportsInboundStockWrite => false;
    public bool SupportsMenuPull => true;
}
