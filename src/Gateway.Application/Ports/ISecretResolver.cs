namespace Gateway.Application.Ports;

/// <summary>
/// Resolves a <c>PosConnection.SecretRef</c> (a Key Vault reference, never a raw
/// credential — ARCHITECTURE.md §7) into the actual secret value. Implemented in
/// Gateway.Infrastructure against Azure Key Vault; adapters never talk to Key
/// Vault directly.
/// </summary>
public interface ISecretResolver
{
    Task<string> ResolveAsync(string secretRef, CancellationToken ct);
}
