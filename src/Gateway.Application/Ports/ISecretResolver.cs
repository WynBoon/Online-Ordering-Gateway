namespace Gateway.Application.Ports;

/// <summary>
    /// Resolves a <c>PosConnection.SecretRef</c> into the actual secret value.
    /// Production uses Key Vault; local development uses configuration / user secrets
    /// when <c>KeyVault:Uri</c> is empty. Adapters never talk to Key Vault directly.
/// </summary>
public interface ISecretResolver
{
    Task<string> ResolveAsync(string secretRef, CancellationToken ct);
}
