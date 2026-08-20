namespace Gateway.Application.Ports;

/// <summary>Outcome of a single call to <see cref="IPosOrderAdapter.CreateOrderAsync"/>.</summary>
public sealed class PosOrderResult
{
    public required bool Success { get; init; }

    /// <summary>The POS's own id for the created order/sale, when successful.</summary>
    public string? PosOrderId { get; init; }

    /// <summary>Machine-readable error code, e.g. "unknown_plu", "store_closed" — mirrors
    /// Order Harmony's own error envelope vocabulary (doc 04) so it can be passed straight through.</summary>
    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    /// <summary>Whether the caller should retry. Drives Order Harmony's own retry/auto-pause
    /// behaviour (ARCHITECTURE.md §6) — this classification matters operationally, not just semantically.</summary>
    public bool Retryable { get; init; }

    public static PosOrderResult Ok(string posOrderId) => new() { Success = true, PosOrderId = posOrderId };

    public static PosOrderResult Fail(string errorCode, string errorMessage, bool retryable) => new()
    {
        Success = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
        Retryable = retryable
    };
}
