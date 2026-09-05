namespace Gateway.Portal.Services;

/// <summary>One dependency probe for the Command Centre health strip.</summary>
public sealed record PlatformHealthItem(string Name, PlatformHealthStatus Status, string? Detail);

public enum PlatformHealthStatus
{
    Ok,
    Degraded,
    Fail,
    NotConfigured
}
