using Gateway.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gateway.Infrastructure.Persistence.Configurations;

public sealed class CanonicalOrderConfiguration : IEntityTypeConfiguration<CanonicalOrder>
{
    public void Configure(EntityTypeBuilder<CanonicalOrder> builder)
    {
        builder.ToTable("Orders");

        // No natural single-column key on the domain type — order_ref is the business
        // key Order Harmony dedupes on, so it's unique but EF still wants a PK; use a
        // shadow identity column.
        builder.Property<long>("InternalId");
        builder.HasKey("InternalId");

        builder.HasIndex(o => o.OrderRef).IsUnique();
        builder.HasIndex(o => new { o.StoreId, o.PlacedAtUtc });

        builder.Property(o => o.Currency).HasMaxLength(3);

        // Items/modifiers are write-once-then-rarely-touched — a JSON column avoids a
        // sprawling item/modifier table structure nothing else needs to query into.
        builder.OwnsMany(o => o.Items, items =>
        {
            items.ToJson();
            items.OwnsMany(i => i.Modifiers);
        });

        builder.OwnsOne(o => o.Customer, c => c.ToJson());
        builder.OwnsOne(o => o.DeliveryAddress, a => a.ToJson());
    }
}
