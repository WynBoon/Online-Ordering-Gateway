using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Ordering.Client;

namespace Ordering.App;

public sealed class SecureStorageOrderingSessionStore : IOrderingSessionStore
{
    public const string StorageKey = "ordering-channel-session";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task<OrderingSession?> GetAsync(CancellationToken ct = default)
    {
        var json = await SecureStorage.Default.GetAsync(StorageKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        var session = JsonSerializer.Deserialize<OrderingSession>(json, Json);
        if (session is not null && GatewayEndpoints.IsLocal(session.GatewayBaseUrl))
        {
            session.GatewayBaseUrl = GatewayEndpoints.UatApi;
            await SaveAsync(session, ct);
        }

        return session;
    }

    public async Task SaveAsync(OrderingSession session, CancellationToken ct = default)
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

internal static class PageServices
{
    public static T Resolve<T>() where T : notnull
    {
        var services = IPlatformApplication.Current?.Services
            ?? throw new InvalidOperationException($"Service {typeof(T).Name} is not registered.");
        return services.GetRequiredService<T>();
    }
}
