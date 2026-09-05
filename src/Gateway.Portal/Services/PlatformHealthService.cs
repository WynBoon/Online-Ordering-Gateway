using Azure.Identity;
using Azure.Messaging.ServiceBus.Administration;
using Azure.Storage.Blobs;
using Gateway.Application.Ports;
using Gateway.Application.Repositories;
using Gateway.Infrastructure.Messaging;
using Gateway.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Gateway.Portal.Services;

/// <summary>
/// Ops-facing dependency probes for the Command Centre. Results are cached briefly
/// so a page refresh does not hammer Azure APIs.
/// </summary>
public sealed class PlatformHealthService(
    GatewayDbContext db,
    IStoreRepository storeRepository,
    IServiceProvider services,
    IConfiguration configuration,
    ILogger<PlatformHealthService> logger)
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private readonly object _gate = new();
    private IReadOnlyList<PlatformHealthItem>? _cached;
    private DateTimeOffset _cachedAt;

    public async Task<IReadOnlyList<PlatformHealthItem>> GetAsync(bool forceRefresh, CancellationToken ct)
    {
        if (!forceRefresh)
        {
            lock (_gate)
            {
                if (_cached is not null && DateTimeOffset.UtcNow - _cachedAt < CacheTtl)
                {
                    return _cached;
                }
            }
        }

        var items = await Task.WhenAll(
            ProbeSqlAsync(ct),
            ProbeServiceBusAsync(ct),
            ProbeStorageAsync(ct),
            ProbePosAsync(ct));

        lock (_gate)
        {
            _cached = items;
            _cachedAt = DateTimeOffset.UtcNow;
            return _cached;
        }
    }

    private async Task<PlatformHealthItem> ProbeSqlAsync(CancellationToken ct)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(ProbeTimeout);
            var ok = await db.Database.CanConnectAsync(timeout.Token);
            return ok
                ? new PlatformHealthItem("SQL", PlatformHealthStatus.Ok, "Gateway database reachable")
                : new PlatformHealthItem("SQL", PlatformHealthStatus.Fail, "CanConnect returned false");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SQL health probe failed");
            return new PlatformHealthItem("SQL", PlatformHealthStatus.Fail, Truncate(ex.Message));
        }
    }

    private async Task<PlatformHealthItem> ProbeServiceBusAsync(CancellationToken ct)
    {
        var connection = configuration.GetConnectionString("ServiceBus");
        var fqns = configuration["ServiceBus:FullyQualifiedNamespace"];

        ServiceBusAdministrationClient? admin = null;
        if (!string.IsNullOrWhiteSpace(connection))
        {
            admin = new ServiceBusAdministrationClient(connection);
        }
        else if (!string.IsNullOrWhiteSpace(fqns))
        {
            admin = new ServiceBusAdministrationClient(fqns, new DefaultAzureCredential());
        }

        if (admin is null)
        {
            return new PlatformHealthItem("Service Bus", PlatformHealthStatus.NotConfigured, "Set ConnectionStrings:ServiceBus or ServiceBus:FullyQualifiedNamespace");
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(ProbeTimeout);
            var topic = await admin.GetTopicAsync(OutboxDispatcher.TopicName, timeout.Token);
            return new PlatformHealthItem(
                "Service Bus",
                PlatformHealthStatus.Ok,
                $"Topic '{topic.Value.Name}' reachable");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Service Bus health probe failed");
            return new PlatformHealthItem("Service Bus", PlatformHealthStatus.Fail, Truncate(ex.Message));
        }
    }

    private async Task<PlatformHealthItem> ProbeStorageAsync(CancellationToken ct)
    {
        var storage = configuration.GetConnectionString("AzureWebJobsStorage")
                      ?? configuration["AzureWebJobsStorage"]
                      ?? configuration.GetConnectionString("Storage");

        if (string.IsNullOrWhiteSpace(storage))
        {
            return new PlatformHealthItem(
                "Storage",
                PlatformHealthStatus.NotConfigured,
                "Set AzureWebJobsStorage (Worker storage) on Portal for this probe");
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(ProbeTimeout);
            var client = new BlobServiceClient(storage);
            var props = await client.GetPropertiesAsync(timeout.Token);
            return new PlatformHealthItem(
                "Storage",
                PlatformHealthStatus.Ok,
                $"Account reachable ({props.Value.DefaultServiceVersion ?? "ok"})");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Storage health probe failed");
            return new PlatformHealthItem("Storage", PlatformHealthStatus.Fail, Truncate(ex.Message));
        }
    }

    private async Task<PlatformHealthItem> ProbePosAsync(CancellationToken ct)
    {
        try
        {
            var stores = await storeRepository.GetActiveStoresAsync(ct);
            if (stores.Count == 0)
            {
                return new PlatformHealthItem("POS API", PlatformHealthStatus.NotConfigured, "No active stores");
            }

            var failures = new List<string>();
            var probed = 0;

            foreach (var store in stores)
            {
                var connection = await storeRepository.GetPosConnectionAsync(store.Id, ct);
                if (connection is null)
                {
                    failures.Add($"{store.Name}: no POS connection");
                    continue;
                }

                probed++;
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(ProbeTimeout);
                try
                {
                    var adapter = services.GetRequiredKeyedService<IPosHealthAdapter>(connection.PosType);
                    var result = await adapter.PingAsync(connection, timeout.Token);
                    if (!result.Healthy)
                    {
                        failures.Add($"{store.Name}: {Truncate(result.Detail) ?? "unhealthy"}");
                    }
                }
                catch (Exception ex)
                {
                    failures.Add($"{store.Name}: {Truncate(ex.Message)}");
                }
            }

            if (probed == 0)
            {
                return new PlatformHealthItem("POS API", PlatformHealthStatus.NotConfigured, "Active stores have no POS connection");
            }

            if (failures.Count == 0)
            {
                return new PlatformHealthItem("POS API", PlatformHealthStatus.Ok, $"{probed} active store(s) OK");
            }

            if (failures.Count < probed)
            {
                return new PlatformHealthItem("POS API", PlatformHealthStatus.Degraded, string.Join("; ", failures));
            }

            return new PlatformHealthItem("POS API", PlatformHealthStatus.Fail, string.Join("; ", failures));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "POS health probe failed");
            return new PlatformHealthItem("POS API", PlatformHealthStatus.Fail, Truncate(ex.Message));
        }
    }

    private static string? Truncate(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length <= 200 ? value : value[..200];
    }
}
