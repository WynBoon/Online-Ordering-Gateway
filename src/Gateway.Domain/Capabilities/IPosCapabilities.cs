namespace Gateway.Domain.Capabilities;

/// <summary>
/// What a given POS adapter can actually do. The application layer reads these
/// flags and adapts behaviour instead of branching on <c>PosType</c> throughout
/// the order pipeline (ARCHITECTURE.md §3). Every adapter implements this once.
/// </summary>
public interface IPosCapabilities
{
    /// <summary>
    /// True for Pilot (per-order callback). When false, preparing/ready come from
    /// an onboarded in-store device; GaapStatusSynthesizer may still confirm
    /// Completed from TENDERED.
    /// </summary>
    bool SupportsRealtimeOrderStatus { get; }

    /// <summary>
    /// True for GAAP: an order can only be injected as an already-closed,
    /// already-paid sale. Drives which use-case path Gateway.Application takes.
    /// </summary>
    bool RequiresPrepaidClosedSale { get; }

    /// <summary>Unconfirmed for both POS today — assume false until proven otherwise.</summary>
    bool SupportsInboundStockWrite { get; }

    bool SupportsMenuPull { get; }
}
