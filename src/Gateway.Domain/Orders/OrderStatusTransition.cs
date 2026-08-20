using Gateway.Domain.Enums;

namespace Gateway.Domain.Orders;

/// <summary>
/// Enforces the order lifecycle Order Harmony expects (doc 02): a fixed sequence
/// with cancellation possible from any non-terminal state, and terminal states
/// that never regress (ARCHITECTURE.md §6 — ordering isn't guaranteed on delivery,
/// so a stale retried event must never undo a later one).
/// </summary>
public static class OrderStatusTransition
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> Allowed = new()
    {
        [OrderStatus.Accepted] = [OrderStatus.Preparing, OrderStatus.Cancelled],
        [OrderStatus.Preparing] = [OrderStatus.Ready, OrderStatus.Cancelled],
        [OrderStatus.Ready] = [OrderStatus.Completed, OrderStatus.Cancelled],
        [OrderStatus.Completed] = [],
        [OrderStatus.Cancelled] = []
    };

    public static bool IsTerminal(OrderStatus status) =>
        status is OrderStatus.Completed or OrderStatus.Cancelled;

    /// <summary>
    /// True if moving from <paramref name="from"/> to <paramref name="to"/> is a
    /// legal transition, or a no-op replay of the same status (idempotent retries
    /// must not be rejected as invalid transitions).
    /// </summary>
    public static bool CanTransition(OrderStatus from, OrderStatus to)
    {
        if (from == to)
        {
            return true;
        }

        return Allowed[from].Contains(to);
    }
}
