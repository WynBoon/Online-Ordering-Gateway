using System.Security.Cryptography;
using System.Text;

namespace Gateway.Domain.Devices;

public static class DeviceSecrets
{
    public static string NewEnrollmentCode()
    {
        var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return value.ToString("D6");
    }

    public static string NewDeviceToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes); // 64 hex chars, uppercase is fine
    }
}
