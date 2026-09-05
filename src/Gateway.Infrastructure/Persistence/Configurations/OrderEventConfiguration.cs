using Gateway.Domain.Events;
using Gateway.Domain.Idempotency;
using Gateway.Domain.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gateway.Infrastructure.Persistence.Configurations;

public sealed class OrderEventConfiguration : IEntityTypeConfiguration<OrderEvent>
{
    public void Configure(EntityTypeBuilder<OrderEvent> builder)
    {
        builder.ToTable("OrderEvents");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.EventId).IsUnique();
        builder.HasIndex(e => e.OrderRef);
        builder.HasIndex(e => e.StoreId);
        builder.HasIndex(e => new { e.StoreId, e.EventTimeUtc });
        builder.HasIndex(e => e.EventTimeUtc);
        builder.Property(e => e.Detail).HasMaxLength(4000);
    }
}

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords");
        builder.HasKey(r => r.IdempotencyKey);
        builder.Property(r => r.IdempotencyKey).HasMaxLength(200);
    }
}

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(m => m.Id);
        builder.HasIndex(m => m.DispatchedAtUtc);
    }
}
