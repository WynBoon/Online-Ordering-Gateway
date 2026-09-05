namespace StoreDevice.Client;

public sealed class MemoryDeviceSessionStore : IDeviceSessionStore
{
    private DeviceSession? _session;

    public Task<DeviceSession?> GetAsync(CancellationToken ct = default) =>
        Task.FromResult(_session);

    public Task SaveAsync(DeviceSession session, CancellationToken ct = default)
    {
        _session = session;
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken ct = default)
    {
        _session = null;
        return Task.CompletedTask;
    }
}
