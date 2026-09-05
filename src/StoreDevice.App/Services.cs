using System.Text.Json;
using StoreDevice.Client;

namespace StoreDevice.App;

public sealed class SecureStorageSessionStore : IDeviceSessionStore
{
    public const string StorageKey = "store-device-session";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task<DeviceSession?> GetAsync(CancellationToken ct = default)
    {
        var json = await SecureStorage.Default.GetAsync(StorageKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<DeviceSession>(json, Json);
    }

    public async Task SaveAsync(DeviceSession session, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(session, Json);
        await SecureStorage.Default.SetAsync(StorageKey, json);
    }

    public Task ClearAsync(CancellationToken ct = default)
    {
        SecureStorage.Default.Remove(StorageKey);
        return Task.CompletedTask;
    }
}

public sealed class OrderSelection
{
    public DeviceOrderDto? Current { get; set; }
}
