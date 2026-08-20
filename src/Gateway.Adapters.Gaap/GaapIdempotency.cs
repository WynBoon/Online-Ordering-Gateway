using System.Security.Cryptography;
using System.Text;

namespace Gateway.Adapters.Gaap;

/// <summary>
/// Derives a stable externalTransactionId from our own order_ref, so a retried
/// call to GAAP is naturally idempotent on GAAP's own dedupe (ARCHITECTURE.md §6)
/// without us having to remember what id we used last time.
/// </summary>
public static class GaapIdempotency
{
    public static string DeriveTransactionId(string orderRef)
    {
        // MD5 here is purely for deterministic id derivation, not a security boundary —
        // any stable hash would do.
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(orderRef));
        return new Guid(hash).ToString();
    }
}
