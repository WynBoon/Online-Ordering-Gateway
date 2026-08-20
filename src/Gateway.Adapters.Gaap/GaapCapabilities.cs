using Gateway.Domain.Capabilities;

namespace Gateway.Adapters.Gaap;

public sealed class GaapCapabilities : IPosCapabilities
{
    public bool SupportsRealtimeOrderStatus => false;
    public bool RequiresPrepaidClosedSale => true;
    public bool SupportsInboundStockWrite => false;
    public bool SupportsMenuPull => true;
}
