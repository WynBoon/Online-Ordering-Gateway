namespace Gateway.Application.Ports;

public sealed class HealthResult
{
    public required bool Healthy { get; init; }
    public string? Detail { get; init; }
    public TimeSpan? Latency { get; init; }
}
