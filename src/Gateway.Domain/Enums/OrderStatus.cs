namespace Gateway.Domain.Enums;

/// <summary>
/// The only status vocabulary that exists anywhere in the system (ARCHITECTURE.md §4).
/// Adapters translate GAAP/Pilot-specific states into this enum; nothing upstream
/// of an adapter invents new values.
/// </summary>
public enum OrderStatus
{
    Accepted,
    Preparing,
    Ready,
    Completed,
    Cancelled
}
