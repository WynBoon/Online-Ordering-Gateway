using Gateway.Domain.Enums;

namespace Gateway.Domain.Tenancy;

/// <summary>
/// Outbound side of a Store's onboarding. <see cref="ExternalNodeId"/>/
/// <see cref="ExternalLocationId"/> hold whichever identifiers the chosen POS
/// needs (GAAP: nodeId/locationId. Pilot: vendorId/siteId). For local/POC the
/// credential may live in <see cref="SecretRef"/> itself; production can still
/// swap that column to a Key Vault URI without changing adapters.
/// </summary>
public sealed class PosConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid StoreId { get; set; }
    public PosType PosType { get; set; }

    public string? ExternalNodeId { get; set; }
    public string? ExternalLocationId { get; set; }

    /// <summary>Pilot API key (or a <c>local://</c> / Key Vault reference resolved by ISecretResolver).</summary>
    public required string SecretRef { get; set; }

    /// <summary>
    /// POS-specific settings that don't generalise across POS types — e.g. GAAP's
    /// terminalId/employeeId/paymentMethodId, none of which have a confirmed
    /// sourcing process yet (ARCHITECTURE.md §10). Deliberately a loose bag rather
    /// than growing this entity per-POS; each adapter documents which keys it reads.
    /// </summary>
    public Dictionary<string, string> ExtraConfig { get; set; } = [];
}
