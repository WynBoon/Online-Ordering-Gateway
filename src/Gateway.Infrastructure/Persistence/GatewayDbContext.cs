using Gateway.Domain.Events;
using Gateway.Domain.Idempotency;
using Gateway.Domain.Orders;
using Gateway.Domain.Outbox;
using Gateway.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Gateway.Infrastructure.Persistence;

/// <summary>Schema matches ARCHITECTURE.md §7 exactly. Domain entities carry no EF
/// attributes — all mapping lives in the Configurations/ folder, keeping
/// Gateway.Domain persistence-ignorant.</summary>
public sealed class GatewayDbContext(DbContextOptions<GatewayDbContext> options) : DbContext(options)
{
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<ChannelConnection> ChannelConnections => Set<ChannelConnection>();
    public DbSet<PosConnection> PosConnections => Set<PosConnection>();
    public DbSet<BillingRate> BillingRates => Set<BillingRate>();
    public DbSet<CanonicalOrder> Orders => Set<CanonicalOrder>();
    public DbSet<OrderEvent> OrderEvents => Set<OrderEvent>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GatewayDbContext).Assembly);
    }
}
