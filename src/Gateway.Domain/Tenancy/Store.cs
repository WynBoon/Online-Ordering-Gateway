using Gateway.Domain.Enums;

namespace Gateway.Domain.Tenancy;

/// <summary>
/// The tenant in this system — a restaurant location, not a platform account
/// (ARCHITECTURE.md §1). Holds one <see cref="ChannelConnection"/> (inbound),
/// one <see cref="PosConnection"/> (outbound), and a billing rate history.
/// Onboarding is: create → optionally attach to a Group → configure both
/// connections → set a billing plan → test connection → activate.
/// </summary>
public sealed class Store
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public Guid? GroupId { get; set; }

    /// <summary>IANA id, e.g. "Africa/Johannesburg". Every UTC timestamp is converted
    /// to this store's local naive time only at the adapter boundary (§7, "Timezones").</summary>
    public required string Timezone { get; set; }

    public StoreState State { get; set; } = StoreState.Draft;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Set when an admin explicitly pauses/unpauses, or the system auto-pauses on repeated failure.</summary>
    public DateTimeOffset? StateChangedAtUtc { get; set; }

    public string? StateChangeReason { get; set; }
}
