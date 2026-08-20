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
}
