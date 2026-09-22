using Gateway.Domain.Devices;

namespace Gateway.Application.Repositories;

public interface IStoreDeviceRepository
{
    Task<StoreDevice?> GetByIdAsync(Guid deviceId, CancellationToken ct);
    Task<StoreDevice?> GetByTokenHashAsync(string tokenHash, CancellationToken ct);
    Task<StoreDevice?> GetPendingByCodeHashAsync(string enrollmentCodeHash, CancellationToken ct);
    Task<IReadOnlyList<StoreDevice>> ListByStoreAsync(Guid storeId, CancellationToken ct);
    Task AddAsync(StoreDevice device, CancellationToken ct);
    Task SaveAsync(StoreDevice device, CancellationToken ct);
    Task DeleteAsync(StoreDevice device, CancellationToken ct);
}
