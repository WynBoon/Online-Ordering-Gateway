using Gateway.Domain.Enums;
using Gateway.Domain.Orders;

namespace Gateway.Domain.Tests;

public class OrderStatusTransitionTests
{
    [Theory]
    [InlineData(OrderStatus.Accepted, OrderStatus.Preparing, true)]
    [InlineData(OrderStatus.Preparing, OrderStatus.Ready, true)]
    [InlineData(OrderStatus.Ready, OrderStatus.Completed, true)]
    [InlineData(OrderStatus.Accepted, OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.Preparing, OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.Ready, OrderStatus.Cancelled, true)]
    public void Allows_forward_and_cancellation_transitions(OrderStatus from, OrderStatus to, bool expected)
    {
        Assert.Equal(expected, OrderStatusTransition.CanTransition(from, to));
    }

    [Theory]
    [InlineData(OrderStatus.Accepted, OrderStatus.Ready)]
    [InlineData(OrderStatus.Accepted, OrderStatus.Completed)]
    [InlineData(OrderStatus.Preparing, OrderStatus.Accepted)]
    [InlineData(OrderStatus.Completed, OrderStatus.Preparing)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Accepted)]
    public void Rejects_skipped_steps_and_regressions_out_of_terminal_states(OrderStatus from, OrderStatus to)
    {
        Assert.False(OrderStatusTransition.CanTransition(from, to));
    }

    [Theory]
    [InlineData(OrderStatus.Accepted)]
    [InlineData(OrderStatus.Preparing)]
    [InlineData(OrderStatus.Completed)]
    public void Same_status_replay_is_always_allowed(OrderStatus status)
    {
        Assert.True(OrderStatusTransition.CanTransition(status, status));
    }

    [Theory]
    [InlineData(OrderStatus.Completed, true)]
    [InlineData(OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.Accepted, false)]
    [InlineData(OrderStatus.Preparing, false)]
    [InlineData(OrderStatus.Ready, false)]
    public void IsTerminal_matches_completed_and_cancelled_only(OrderStatus status, bool expected)
    {
        Assert.Equal(expected, OrderStatusTransition.IsTerminal(status));
    }
}
