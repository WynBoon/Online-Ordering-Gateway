using Gateway.Domain.Enums;

namespace Gateway.Domain.Tenancy;

/// <summary>
/// Outbound side of a Store's onboarding. <see cref="ExternalNodeId"/>/
/// <see cref="ExternalLocationId"/> hold whichever identifiers the chosen POS
/// needs (GAAP: nodeId/locationId. Pilot: vendorId/siteId). The actual secret
/// never lives here — <see cref="SecretRef"/> is a Key Vault reference only
/// (ARCHITECTURE.md §7).
/// </summary>
public sealed class PosConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid StoreId { get; set; }
    public PosType PosType { get; set; }

    public string? ExternalNodeId { get; set; }
    public string? ExternalLocationId { get; set; }

    /// <summary>Key Vault secret name/URI holding the apikey (GAAP) or vendor global key (Pilot).</summary>
    public required string SecretRef { get; set; }

    /// <summary>
    /// POS-specific settings that don't generalise across POS types — e.g. GAAP's
    /// terminalId/employeeId/paymentMethodId, none of which have a confirmed
    /// sourcing process yet (ARCHITECTURE.md §10). Deliberately a loose bag rather
    /// than growing this entity per-POS; each adapter documents which keys it reads.
    /// </summary>
    public Dictionary<string, string> ExtraConfig { get; set; } = [];
}
