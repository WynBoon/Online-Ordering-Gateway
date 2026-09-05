using Gateway.Application.Devices;
using Gateway.Application.Repositories;
using Gateway.Domain.Devices;
using Gateway.Domain.Tenancy;

namespace Gateway.Application.UseCases;

public sealed class DeviceEnrollmentUseCase(
    IStoreDeviceRepository devices,
    IStoreRepository stores)
{
    public static readonly TimeSpan EnrollmentLifetime = TimeSpan.FromMinutes(15);

    public async Task<IssuedEnrollment> IssueAsync(
        Guid storeId,
        DeviceFunction? functions,
        string? name,
        CancellationToken ct)
    {
        var store = await stores.GetByIdAsync(storeId, ct)
            ?? throw new InvalidOperationException("Store not found.");

        var granted = await ResolveGrantedFunctionsAsync(store, functions, ct);
        var code = DeviceSecrets.NewEnrollmentCode();
        var now = DateTimeOffset.UtcNow;
        var trimmedName = string.IsNullOrWhiteSpace(name) ? "Unnamed tablet" : name.Trim();

        var device = new StoreDevice
        {
            StoreId = storeId,
            Name = trimmedName,
            Status = DeviceStatus.PendingEnrollment,
            Functions = granted,
            EnrollmentCodeHash = DeviceSecrets.Hash(code),
            EnrollmentExpiresAtUtc = now + EnrollmentLifetime,
            CreatedAtUtc = now
        };

        await devices.AddAsync(device, ct);

        return new IssuedEnrollment(device.Id, code, device.EnrollmentExpiresAtUtc.Value);
    }

    public async Task<ClaimedEnrollment> ClaimAsync(
        string enrollmentCode,
        string? deviceName,
        string? hardwareFingerprint,
        CancellationToken ct)
    {
        var code = enrollmentCode?.Trim() ?? "";
        if (code.Length != 6)
        {
            throw new InvalidOperationException("Enrollment code is not valid.");
        }

        var pending = await devices.GetPendingByCodeHashAsync(DeviceSecrets.Hash(code), ct);
        if (pending is null)
        {
            throw new InvalidOperationException("Enrollment code is not valid.");
        }

        var now = DateTimeOffset.UtcNow;
        if (pending.EnrollmentExpiresAtUtc is { } expires && expires < now)
        {
            pending.Status = DeviceStatus.Revoked;
            pending.EnrollmentCodeHash = null;
            pending.EnrollmentExpiresAtUtc = null;
            pending.RevokedAtUtc = now;
            await devices.SaveAsync(pending, ct);
            throw new InvalidOperationException("Enrollment code has expired. Issue a new one from the portal.");
        }

        var store = await stores.GetByIdAsync(pending.StoreId, ct)
            ?? throw new InvalidOperationException("Store not found.");

        var token = DeviceSecrets.NewDeviceToken();
        var claimedName = string.IsNullOrWhiteSpace(deviceName) ? pending.Name : deviceName.Trim();

        pending.Status = DeviceStatus.Active;
        pending.Name = claimedName;
        pending.TokenHash = DeviceSecrets.Hash(token);
        pending.HardwareFingerprint = string.IsNullOrWhiteSpace(hardwareFingerprint)
            ? pending.HardwareFingerprint
            : hardwareFingerprint.Trim();
        pending.EnrollmentCodeHash = null;
        pending.EnrollmentExpiresAtUtc = null;
        pending.EnrolledAtUtc = now;
        pending.LastHeartbeatAtUtc = now;
        await devices.SaveAsync(pending, ct);

        return new ClaimedEnrollment(
            pending.Id, pending.StoreId, store.Name, pending.Name, token, pending.Functions);
    }

    public async Task UpdateFunctionsAsync(Guid deviceId, DeviceFunction functions, CancellationToken ct)
    {
        var device = await devices.GetByIdAsync(deviceId, ct)
            ?? throw new InvalidOperationException("Device not found.");
        device.Functions = functions;
        await devices.SaveAsync(device, ct);
    }

    public async Task RevokeAsync(Guid deviceId, CancellationToken ct)
    {
        var device = await devices.GetByIdAsync(deviceId, ct)
            ?? throw new InvalidOperationException("Device not found.");
        device.Status = DeviceStatus.Revoked;
        device.RevokedAtUtc = DateTimeOffset.UtcNow;
        device.TokenHash = null;
        device.EnrollmentCodeHash = null;
        device.EnrollmentExpiresAtUtc = null;
        await devices.SaveAsync(device, ct);
    }

    public async Task HeartbeatAsync(Guid deviceId, CancellationToken ct)
    {
        var device = await devices.GetByIdAsync(deviceId, ct)
            ?? throw new InvalidOperationException("Device not found.");
        if (device.Status != DeviceStatus.Active)
        {
            throw new InvalidOperationException("Device is not active.");
        }

        device.LastHeartbeatAtUtc = DateTimeOffset.UtcNow;
        await devices.SaveAsync(device, ct);
    }

    public async Task<DeviceSessionDto> ToSessionAsync(Guid deviceId, CancellationToken ct)
    {
        var device = await devices.GetByIdAsync(deviceId, ct)
            ?? throw new InvalidOperationException("Device not found.");
        var store = await stores.GetByIdAsync(device.StoreId, ct)
            ?? throw new InvalidOperationException("Store not found.");
        return ToSession(device, store, DateTimeOffset.UtcNow);
    }

    public static DeviceSessionDto ToSession(StoreDevice device, Store store, DateTimeOffset utcNow) =>
        new(
            device.Id,
            device.StoreId,
            store.Name,
            device.Name,
            device.Status,
            device.Functions,
            device.IsOnline(utcNow),
            device.LastHeartbeatAtUtc);

    private async Task<DeviceFunction> ResolveGrantedFunctionsAsync(
        Store store,
        DeviceFunction? functions,
        CancellationToken ct)
    {
        if (functions is { } provided)
        {
            return provided;
        }

        if (store.DefaultDeviceFunctions != DeviceFunction.None)
        {
            return store.DefaultDeviceFunctions;
        }

        var pos = await stores.GetPosConnectionAsync(store.Id, ct);
        if (pos is not null)
        {
            return DeviceFunctionSets.RecommendedFor(pos.PosType);
        }

        return DeviceFunctionSets.KitchenStatusOfRecord;
    }
}
