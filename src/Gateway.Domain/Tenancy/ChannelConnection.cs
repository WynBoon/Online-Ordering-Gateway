using Gateway.Domain.Enums;

namespace Gateway.Domain.Tenancy;

/// <summary>
/// Inbound side of a Store's onboarding. Modelled generically so a second
/// channel isn't a schema change (ARCHITECTURE.md §7).
/// </summary>
public sealed class ChannelConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid StoreId { get; set; }
    public ChannelType ChannelType { get; set; }

    /// <summary>The Bearer location key we issued to the merchant, entered into the channel's integration screen.</summary>
    public required string LocationKey { get; set; }

    /// <summary>Set at rotation time; the old key is still accepted for 24h per doc 04 §1.</summary>
    public string? PreviousLocationKey { get; set; }
    public DateTimeOffset? LocationKeyRotatedAtUtc { get; set; }

    /// <summary>Issued per environment during onboarding (doc 02 §2) — where we POST
    /// outbound status webhooks for this store.</summary>
    public required string WebhookUrl { get; set; }

    /// <summary>Key Vault reference to the HMAC signing secret for this connection's
    /// webhooks (doc 02 §4) — never the raw secret.</summary>
    public required string SigningSecretRef { get; set; }
}
