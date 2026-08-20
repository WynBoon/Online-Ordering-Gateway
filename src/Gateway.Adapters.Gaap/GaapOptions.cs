namespace Gateway.Adapters.Gaap;

/// <summary>Bound from configuration (Gateway.Api/Worker appsettings). The apikey
/// itself is never here — it's resolved per-connection from Key Vault via
/// PosConnection.SecretRef (ARCHITECTURE.md §7).</summary>
public sealed class GaapOptions
{
    public const string SectionName = "Gaap";

    public string BaseUrl { get; set; } = "https://data-api.gaapunity.app";

    /// <summary>How often the status synthesizer polls GET /sales for a pending order.</summary>
    public TimeSpan StatusPollInterval { get; set; } = TimeSpan.FromMinutes(5);
}
