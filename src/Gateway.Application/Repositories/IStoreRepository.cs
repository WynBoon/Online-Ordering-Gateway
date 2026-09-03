using Gateway.Domain.Tenancy;

namespace Gateway.Application.Repositories;

public interface IStoreRepository
{
    Task<Store?> GetByIdAsync(Guid storeId, CancellationToken ct);

    /// <summary>Resolves a Store from the Order Harmony location key on an inbound request.</summary>
    Task<Store?> GetByLocationKeyAsync(string locationKey, CancellationToken ct);

    Task<ChannelConnection?> GetChannelConnectionAsync(Guid storeId, CancellationToken ct);
    Task<PosConnection?> GetPosConnectionAsync(Guid storeId, CancellationToken ct);

    /// <summary>Used by the scheduled menu re-pull (ARCHITECTURE.md §11 Phase 4) — only
    /// Active stores are worth re-pulling; see the Store lifecycle gate in §7.</summary>
    Task<IReadOnlyList<Store>> GetActiveStoresAsync(CancellationToken ct);

    /// <summary>Every store regardless of state — the command centre's fleet grid needs
    /// to show Draft/Paused/Deactivated too, not just Active (UI-ARCHITECTURE.md).</summary>
    Task<IReadOnlyList<Store>> GetAllAsync(CancellationToken ct);

    Task CreateAsync(Store store, CancellationToken ct);
    Task UpdateStateAsync(Guid storeId, Domain.Enums.StoreState newState, string? reason, CancellationToken ct);

    /// <summary>Insert or replace the store's outbound POS connection.</summary>
    Task SavePosConnectionAsync(PosConnection connection, CancellationToken ct);
}
