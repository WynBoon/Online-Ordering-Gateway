namespace Gateway.Application.UseCases;

public sealed class OrderInjectionResult
{
    public required bool Success { get; init; }
    public string? PosOrderId { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public bool Retryable { get; init; }

    public static OrderInjectionResult Ok(string posOrderId) => new() { Success = true, PosOrderId = posOrderId };

    public static OrderInjectionResult Fail(string errorCode, string errorMessage, bool retryable) => new()
    {
        Success = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
        Retryable = retryable
    };
}
