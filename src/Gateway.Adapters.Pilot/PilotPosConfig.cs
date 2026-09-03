using System.Text.Json;
using Gateway.Domain.Enums;
using Gateway.Domain.Tenancy;

namespace Gateway.Adapters.Pilot;

/// <summary>
/// How a Pilot token probe is stored on <see cref="PosConnection"/>: vendor/site on the
/// generic id fields, last-seen permissions in ExtraConfig. The API key is SecretRef.
/// </summary>
public static class PilotPosConfig
{
    public const string PermissionsKey = "pilot.lastPermissions";
    public const string ProbedAtKey = "pilot.lastProbedAtUtc";

    public static void ApplyProbe(PosConnection connection, PilotConnectionProbe probe, string? apiKey)
    {
        connection.PosType = PosType.Pilot;
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            connection.SecretRef = apiKey.Trim();
        }

        connection.ExternalNodeId = probe.VendorId;
        connection.ExternalLocationId = probe.StoreId;

        var extra = new Dictionary<string, string>(connection.ExtraConfig);
        extra[PermissionsKey] = JsonSerializer.Serialize(probe.Permissions);
        extra[ProbedAtKey] = DateTimeOffset.UtcNow.ToString("O");
        connection.ExtraConfig = extra;
    }

    public static IReadOnlyList<string> ReadPermissions(PosConnection connection)
    {
        if (!connection.ExtraConfig.TryGetValue(PermissionsKey, out var json) || string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<string>>(json) ?? [];
    }

    public static DateTimeOffset? ReadProbedAt(PosConnection connection)
    {
        if (!connection.ExtraConfig.TryGetValue(ProbedAtKey, out var value) ||
            !DateTimeOffset.TryParse(value, out var probedAt))
        {
            return null;
        }

        return probedAt;
    }
}
