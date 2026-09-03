using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Gateway.Infrastructure.Persistence;

/// <summary>
/// Development-only: apply EF migrations and seed the local stores. Called from
/// each host at startup so <c>dotnet run</c> is enough — no separate
/// <c>dotnet ef database update</c> step. Production must never hit this.
/// </summary>
public static class DevelopmentHost
{
    public static async Task InitializeAsync(
        IServiceProvider services,
        IConfiguration configuration,
        bool isDevelopment,
        CancellationToken ct = default)
    {
        if (!isDevelopment)
        {
            return;
        }

        if (string.Equals(configuration["Development:ApplyMigrationsAndSeed"], "false", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var connection = configuration.GetConnectionString("Gateway");
        if (string.IsNullOrWhiteSpace(connection))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Gateway is empty. For local dev it should point at LocalDB — see docs/LOCAL-DEV.md.");
        }

        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger("DevelopmentHost");
        var db = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();

        try
        {
            await db.Database.MigrateAsync(ct);
        }
        catch (Exception ex) when (LooksLikeUnreachableSql(ex))
        {
            throw new InvalidOperationException(
                "Could not reach SQL Server. Local dev expects Docker SQL on localhost,1433 " +
                "(`docker compose up -d sql`). See docs/LOCAL-DEV.md.",
                ex);
        }

        await DevelopmentStoreSeeder.SeedIfEmptyAsync(db, ct);
        logger?.LogInformation(
            "Local dev database ready. Active store location key: {LocationKey}",
            DevelopmentStoreSeeder.ActiveLocationKey);
    }

    private static bool LooksLikeUnreachableSql(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (e.GetType().Name is "SqlException" or "Win32Exception")
            {
                return true;
            }

            if (e.Message.Contains("Local Database Runtime", StringComparison.OrdinalIgnoreCase) ||
                e.Message.Contains("LocalDB", StringComparison.OrdinalIgnoreCase) ||
                e.Message.Contains("network-related", StringComparison.OrdinalIgnoreCase) ||
                e.Message.Contains("error: 40", StringComparison.OrdinalIgnoreCase) ||
                e.Message.Contains("error: 52", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
