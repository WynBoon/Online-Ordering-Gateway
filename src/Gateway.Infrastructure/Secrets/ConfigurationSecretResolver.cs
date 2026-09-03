using Gateway.Application.Ports;
using Microsoft.Extensions.Configuration;

namespace Gateway.Infrastructure.Secrets;

/// <summary>
/// Local-dev stand-in for Key Vault. Secret refs of the form <c>local://name</c>
/// resolve to <c>LocalSecrets:name</c> in configuration (typically user secrets).
/// Any other value is treated as the literal secret — the portal stores the Pilot
/// API key on <c>PosConnection.SecretRef</c>. Production still uses
/// <see cref="KeyVaultSecretResolver"/> when <c>KeyVault:Uri</c> is set.
/// </summary>
public sealed class ConfigurationSecretResolver(IConfiguration configuration) : ISecretResolver
{
    public const string RefPrefix = "local://";

    public Task<string> ResolveAsync(string secretRef, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretRef);

        if (secretRef.StartsWith(RefPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var key = secretRef[RefPrefix.Length..];
            var value = configuration[$"LocalSecrets:{key}"];
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"No value for LocalSecrets:{key}. For local dev run: " +
                    $"dotnet user-secrets set \"LocalSecrets:{key}\" \"<value>\" --project src/Gateway.Api " +
                    $"(and the same for Gateway.Portal / Gateway.Worker). See docs/LOCAL-DEV.md.");
            }

            return Task.FromResult(value);
        }

        // Portal POS config stores the Pilot API key on PosConnection itself.
        return Task.FromResult(secretRef);
    }
}
