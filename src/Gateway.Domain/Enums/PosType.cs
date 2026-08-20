namespace Gateway.Domain.Enums;

/// <summary>
/// Which POS a store's outbound <c>PosConnection</c> is wired to. Adding a third
/// POS means adding a value here plus a new adapter — the ports in
/// Gateway.Application don't change.
/// </summary>
public enum PosType
{
    Gaap,
    Pilot
}
