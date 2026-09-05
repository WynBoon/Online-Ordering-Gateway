namespace Gateway.Application.Ports;

/// <summary>Outcome of a single call to <see cref="IPosOrderAdapter.CreateOrderAsync"/>.</summary>
public sealed class PosOrderResult
{
    public const int MaxDetailLength = 4000;

    public required bool Success { get; init; }

    /// <summary>The POS's own id for the created order/sale, when successful.</summary>
    public string? PosOrderId { get; init; }

    /// <summary>Machine-readable error code, e.g. "unknown_plu", "store_closed" — mirrors
    /// Order Harmony's own error envelope vocabulary (doc 04) so it can be passed straight through.</summary>
    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    /// <summary>Optional POS message / truncated body text for observability (OrderEvent.Detail).</summary>
    public string? Detail { get; init; }

    /// <summary>Whether the caller should retry. Drives Order Harmony's own retry/auto-pause
    /// behaviour (ARCHITECTURE.md §6) — this classification matters operationally, not just semantically.</summary>
    public bool Retryable { get; init; }

    public static PosOrderResult Ok(string posOrderId, string? detail = null) => new()
    {
        Success = true,
        PosOrderId = posOrderId,
        Detail = Truncate(detail)
    };

    public static PosOrderResult Fail(string errorCode, string errorMessage, bool retryable, string? detail = null) => new()
    {
        Success = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
        Detail = Truncate(detail ?? errorMessage),
        Retryable = retryable
    };

    public static string? Truncate(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length <= MaxDetailLength ? value : value[..MaxDetailLength];
    }
}
