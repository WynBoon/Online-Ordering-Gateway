namespace Gateway.Adapters.Pilot;

public sealed class PilotOptions
{
    public const string SectionName = "Pilot";

    public string BaseUrl { get; set; } = "https://openapi.pilotlive.co.za";

    /// <summary>Our own public base URL, used to build the callbackUrl we hand Pilot per
    /// order — e.g. "https://gateway-api.example.com".</summary>
    public string CallbackBaseUrl { get; set; } = "";

    /// <summary>Refresh the JWT this long before its declared expiry.</summary>
    public TimeSpan TokenRefreshMargin { get; set; } = TimeSpan.FromMinutes(2);
}
