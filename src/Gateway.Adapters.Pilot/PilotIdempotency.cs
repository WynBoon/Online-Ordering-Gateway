using System.Security.Cryptography;
using System.Text;

namespace Gateway.Adapters.Pilot;

/// <summary>
/// Derives a stable numeric orderId from our order_ref. Pilot's idempotency
/// behaviour on create-replay isn't documented (open question, ARCHITECTURE.md
/// §10) — a deterministic id at least gives Pilot a consistent value to key off
/// if/when they confirm how their own dedupe works.
/// </summary>
public static class PilotIdempotency
{
    public static long DeriveOrderId(string orderRef)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(orderRef));
        // Mask off the sign bit so this is always a positive long.
        return BitConverter.ToInt64(hash, 0) & 0x7FFFFFFFFFFFFFFF;
    }
}
