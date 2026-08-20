using Gateway.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gateway.Infrastructure.Persistence.Configurations;

public sealed class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable("Groups");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Name).HasMaxLength(200);
    }
}

public sealed class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> builder)
    {
        builder.ToTable("Stores");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).HasMaxLength(200);
        builder.Property(s => s.Timezone).HasMaxLength(100);
        builder.HasIndex(s => s.GroupId);
    }
}

public sealed class ChannelConnectionConfiguration : IEntityTypeConfiguration<ChannelConnection>
{
    public void Configure(EntityTypeBuilder<ChannelConnection> builder)
    {
        builder.ToTable("ChannelConnections");
        builder.HasKey(c => c.Id);
        // One channel connection per store today — enforced here even though the
        // model is generic enough to relax later (ARCHITECTURE.md §7).
        builder.HasIndex(c => c.StoreId).IsUnique();
        builder.HasIndex(c => c.LocationKey).IsUnique();
        builder.Property(c => c.LocationKey).HasMaxLength(200);
    }
}

public sealed class PosConnectionConfiguration : IEntityTypeConfiguration<PosConnection>
{
    public void Configure(EntityTypeBuilder<PosConnection> builder)
    {
        builder.ToTable("PosConnections");
        builder.HasKey(c => c.Id);
        builder.HasIndex(c => c.StoreId).IsUnique();
        builder.Property(c => c.ExtraConfig).HasColumnType("nvarchar(max)").HasConversion(
            v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
            v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new(),
            new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<Dictionary<string, string>>(
                (a, b) => (a ?? new()).SequenceEqual(b ?? new()),
                d => d.Aggregate(0, (hash, kvp) => HashCode.Combine(hash, kvp.Key, kvp.Value)),
                d => new Dictionary<string, string>(d)));
    }
}

public sealed class BillingRateConfiguration : IEntityTypeConfiguration<BillingRate>
{
    public void Configure(EntityTypeBuilder<BillingRate> builder)
    {
        builder.ToTable("BillingRates");
        builder.HasKey(r => r.Id);
        builder.HasIndex(r => new { r.StoreId, r.EffectiveFrom });
    }
}
