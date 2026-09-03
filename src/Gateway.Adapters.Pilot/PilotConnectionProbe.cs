namespace Gateway.Adapters.Pilot;

/// <summary>
/// Identity and capability snapshot from POST /Authorization/Token. The JWT itself
/// is not included — it expires and is fetched again at call time.
/// </summary>
public sealed class PilotConnectionProbe
{
    public const string OnlineOrdersPermission = "OnlineOrders";

    public required string VendorId { get; init; }
    public required string StoreId { get; init; }
    public IReadOnlyList<string> Permissions { get; init; } = [];
    public string? TokenType { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }

    public bool HasOnlineOrders =>
        Permissions.Contains(OnlineOrdersPermission, StringComparer.OrdinalIgnoreCase);
}
