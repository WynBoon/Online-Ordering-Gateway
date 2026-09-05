using Gateway.Domain.Enums;
using Gateway.Domain.Events;
using Gateway.Domain.Orders;

namespace Gateway.Application.Repositories;

public interface IOrderRepository
{
    Task<CanonicalOrder?> GetByOrderRefAsync(string orderRef, CancellationToken ct);
    Task SaveAsync(CanonicalOrder order, CancellationToken ct);
    Task AppendEventAsync(OrderEvent orderEvent, CancellationToken ct);

    /// <summary>Non-terminal orders at stores wired to the given POS type — used by the
    /// GAAP status synthesizer's poll (ARCHITECTURE.md §5). Pilot never needs this since
    /// its callback pushes status changes directly.</summary>
    Task<IReadOnlyList<CanonicalOrder>> GetPendingByPosTypeAsync(PosType posType, CancellationToken ct);

    Task<IReadOnlyList<OrderEvent>> GetRecentEventsAsync(int take, CancellationToken ct);
    Task<IReadOnlyList<OrderEvent>> GetRecentEventsByStoreAsync(Guid storeId, int take, CancellationToken ct);
    Task<IReadOnlyList<OrderEvent>> GetEventsByOrderRefAsync(string orderRef, CancellationToken ct);
    Task<IReadOnlyList<CanonicalOrder>> GetRecentOrdersAsync(int take, CancellationToken ct);
    Task<IReadOnlyList<CanonicalOrder>> GetRecentOrdersByStoreAsync(Guid storeId, int take, CancellationToken ct);
    Task<int> CountOrdersTodayAsync(CancellationToken ct);

    /// <summary>Interim UAT KPI: share of success outcomes among injection/status
    /// events in the last hour. Null when there were no relevant events.</summary>
    Task<double?> GetSuccessRateLastHourAsync(CancellationToken ct);
}
