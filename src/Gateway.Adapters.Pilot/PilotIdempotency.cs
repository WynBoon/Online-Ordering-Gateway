using System.Security.Cryptography;
using System.Text;

namespace Gateway.Adapters.Pilot;

/// <summary>
/// Derives a stable numeric orderId from our order_ref. Pilot's create body
/// declares <c>orderId</c> as int32 — the value must fit that or QA returns 400.
/// Idempotency on replay is still undocumented (ARCHITECTURE.md §10).
/// </summary>
public static class PilotIdempotency
{
    public static int DeriveOrderId(string orderRef)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(orderRef));
        return (int)(BitConverter.ToUInt32(hash, 0) & 0x7FFFFFFF);
    }
}
