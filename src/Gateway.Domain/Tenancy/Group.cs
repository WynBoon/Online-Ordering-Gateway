namespace Gateway.Domain.Tenancy;

/// <summary>
/// Optional ownership grouping above <see cref="Store"/> — a franchise or
/// multi-site owner. Purely organisational: consolidated reporting/billing
/// rollups and shared config defaults a Store can inherit and override. A
/// Store can stand alone with no Group (ARCHITECTURE.md §7).
/// </summary>
public sealed class Group
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
