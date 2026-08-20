using Gateway.Domain.Enums;

namespace Gateway.Domain.Tenancy;

/// <summary>
/// Append-only rate history — never a single mutable field on Store. A rate
/// change must never retroactively alter what an already-issued invoice was
/// based on (ARCHITECTURE.md §7). The current rate for a store is the row
/// where <see cref="EffectiveFrom"/> is most recent and <see cref="EffectiveTo"/>
/// is null or in the future.
/// </summary>
public sealed class BillingRate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid StoreId { get; set; }
    public BillingPlanType PlanType { get; set; }

    /// <summary>Flat: cents per billing period. PerTransaction: cents per order.</summary>
    public long RateCents { get; set; }

    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
}
