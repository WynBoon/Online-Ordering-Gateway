using Gateway.Application.Repositories;
using Gateway.Domain.Devices;
using Gateway.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Gateway.Infrastructure.Repositories;

public sealed class StoreDeviceRepository(GatewayDbContext db) : IStoreDeviceRepository
{
    public Task<StoreDevice?> GetByIdAsync(Guid deviceId, CancellationToken ct) =>
        db.StoreDevices.FirstOrDefaultAsync(d => d.Id == deviceId, ct);

    public Task<StoreDevice?> GetByTokenHashAsync(string tokenHash, CancellationToken ct) =>
        db.StoreDevices.FirstOrDefaultAsync(
            d => d.TokenHash == tokenHash && d.Status == DeviceStatus.Active, ct);

    public Task<StoreDevice?> GetPendingByCodeHashAsync(string enrollmentCodeHash, CancellationToken ct) =>
        db.StoreDevices.FirstOrDefaultAsync(
            d => d.EnrollmentCodeHash == enrollmentCodeHash && d.Status == DeviceStatus.PendingEnrollment, ct);

    public async Task<IReadOnlyList<StoreDevice>> ListByStoreAsync(Guid storeId, CancellationToken ct) =>
        await db.StoreDevices
            .Where(d => d.StoreId == storeId)
            .OrderBy(d => d.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task AddAsync(StoreDevice device, CancellationToken ct)
    {
        db.StoreDevices.Add(device);
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveAsync(StoreDevice device, CancellationToken ct)
    {
        db.StoreDevices.Update(device);
        await db.SaveChangesAsync(ct);
    }
}
