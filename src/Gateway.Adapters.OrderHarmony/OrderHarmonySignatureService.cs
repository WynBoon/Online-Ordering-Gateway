using System.Security.Cryptography;
using System.Text;

namespace Gateway.Adapters.OrderHarmony;

/// <summary>
/// Signs outbound webhooks exactly as doc 02 §4 specifies: lowercase hex
/// HMAC-SHA256(secret, "{timestamp}.{raw_body}"), signing the raw body bytes
/// before any re-serialisation.
/// </summary>
public static class OrderHarmonySignatureService
{
    public static string Sign(string secret, long unixTimestamp, string rawBody)
    {
        var message = $"{unixTimestamp}.{rawBody}";
        var key = Encoding.UTF8.GetBytes(secret);
        var hash = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(message));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
