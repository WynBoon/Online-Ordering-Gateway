using Gateway.Domain.Enums;
using Gateway.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Gateway.Infrastructure.Persistence;

/// <summary>
/// Idempotent local-dev seed so the API has a Bearer location key to authenticate
/// against and the portal command centre isn't an empty grid. Never runs outside
/// Development — see <see cref="DevelopmentHost"/>.
/// </summary>
public static class DevelopmentStoreSeeder
{
    public static readonly Guid ActiveStoreId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    public static readonly Guid PausedStoreId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");

    public const string ActiveLocationKey = "dev-local-location-key";
    public const string PausedLocationKey = "dev-paused-location-key";

    public const string SigningSecretRef = "local://oh-signing-secret";
    public const string PilotSecretRef = "local://pilot-api-key";

    public static async Task SeedIfEmptyAsync(GatewayDbContext db, CancellationToken ct)
    {
        if (await db.Stores.AnyAsync(s => s.Id == ActiveStoreId, ct))
        {
            return;
        }

        db.Stores.Add(new Store
        {
            Id = ActiveStoreId,
            Name = "Local Dev Kitchen",
            Timezone = "Africa/Johannesburg",
            State = StoreState.Active,
            StateChangeReason = "Seeded for local development"
        });
        db.ChannelConnections.Add(new ChannelConnection
        {
            StoreId = ActiveStoreId,
            ChannelType = ChannelType.OrderHarmony,
            LocationKey = ActiveLocationKey,
            WebhookUrl = "http://127.0.0.1:9/order-harmony-webhooks",
            SigningSecretRef = SigningSecretRef
        });
        db.PosConnections.Add(new PosConnection
        {
            StoreId = ActiveStoreId,
            PosType = PosType.Pilot,
            ExternalNodeId = "dev-vendor",
            ExternalLocationId = "dev-site",
            SecretRef = PilotSecretRef
        });
        db.BillingRates.Add(new BillingRate
        {
            StoreId = ActiveStoreId,
            PlanType = BillingPlanType.PerTransaction,
            RateCents = 150,
            EffectiveFrom = DateTimeOffset.UtcNow
        });

        db.Stores.Add(new Store
        {
            Id = PausedStoreId,
            Name = "Local Dev Cafe (Paused)",
            Timezone = "Africa/Johannesburg",
            State = StoreState.Paused,
            StateChangedAtUtc = DateTimeOffset.UtcNow,
            StateChangeReason = "Seeded paused so the command centre has an exception to surface"
        });
        db.ChannelConnections.Add(new ChannelConnection
        {
            StoreId = PausedStoreId,
            ChannelType = ChannelType.OrderHarmony,
            LocationKey = PausedLocationKey,
            WebhookUrl = "http://127.0.0.1:9/order-harmony-webhooks",
            SigningSecretRef = SigningSecretRef
        });
        db.PosConnections.Add(new PosConnection
        {
            StoreId = PausedStoreId,
            PosType = PosType.Pilot,
            ExternalNodeId = "dev-vendor",
            ExternalLocationId = "dev-site-paused",
            SecretRef = PilotSecretRef
        });

        await db.SaveChangesAsync(ct);
    }
}
