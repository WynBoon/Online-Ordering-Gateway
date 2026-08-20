using Gateway.Domain.Enums;

namespace Gateway.Adapters.Pilot;

/// <summary>
/// Pilot's <c>orderStatus.statusCode</c> integer table isn't published — the only
/// confirmed data point from their swagger is <c>2 = "Pending"</c> (open question,
/// ARCHITECTURE.md §10). Everything below is a placeholder to be replaced once
/// Pilot confirms the real table; unmapped codes are deliberately rejected
/// rather than guessed into a status, since guessing wrong here means reporting
/// a false status to Order Harmony.
/// </summary>
public static class PilotStatusCodeMapping
{
    public static bool TryMap(int statusCode, out OrderStatus status)
    {
        switch (statusCode)
        {
            case 2: // "Pending" per the one confirmed example — treated as Accepted
                status = OrderStatus.Accepted;
                return true;
            default:
                status = default;
                return false;
        }
    }
}
