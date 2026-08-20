using Azure.Security.KeyVault.Secrets;
using Gateway.Application.Ports;

namespace Gateway.Infrastructure.Secrets;

/// <summary>
/// Resolves a PosConnection/ChannelConnection SecretRef into its actual value.
/// Uses managed identity (via DI-registered SecretClient with DefaultAzureCredential)
/// — never a connection string with an embedded secret (ARCHITECTURE.md §8, §13).
/// </summary>
public sealed class KeyVaultSecretResolver(SecretClient secretClient) : ISecretResolver
{
    public async Task<string> ResolveAsync(string secretRef, CancellationToken ct)
    {
        var secret = await secretClient.GetSecretAsync(secretRef, cancellationToken: ct);
        return secret.Value.Value;
    }
}
