using System.Text.Json.Serialization;

namespace Gateway.Adapters.OrderHarmony.Dtos;

/// <summary>
/// Mirrors doc 04 §3 exactly. <see cref="Retryable"/> is what drives Order
/// Harmony's queue — always include it (ARCHITECTURE.md §6).
/// </summary>
public sealed class ErrorEnvelope
{
    [JsonPropertyName("code")]
    public required string Code { get; set; }

    [JsonPropertyName("message")]
    public required string Message { get; set; }

    [JsonPropertyName("retryable")]
    public required bool Retryable { get; set; }

    public static class Codes
    {
        public const string InvalidPayload = "invalid_payload";
        public const string Unauthorized = "unauthorized";
        public const string UnknownPlu = "unknown_plu";
        public const string UnknownLocation = "unknown_location";
        public const string DuplicateOrder = "duplicate_order";
        public const string StoreClosed = "store_closed";
        public const string ModifierRuleViolation = "modifier_rule_violation";
        public const string RateLimited = "rate_limited";
        public const string TillOffline = "till_offline";
    }
}
