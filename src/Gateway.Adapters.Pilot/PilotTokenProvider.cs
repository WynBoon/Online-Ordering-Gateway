using System.Collections.Concurrent;
using System.Net.Http.Json;
using Gateway.Adapters.Pilot.Dtos;
using Gateway.Application.Ports;
using Gateway.Domain.Tenancy;
using Microsoft.Extensions.Options;

namespace Gateway.Adapters.Pilot;

/// <summary>
/// Exchanges the vendor global API key for a short-lived JWT (doc: POST
/// /Authorization/Token) and caches it in memory per connection until shortly
/// before expiry. Simple in-process cache is fine at this scale — see
/// ARCHITECTURE.md §15 on not over-building for load that doesn't exist yet.
/// </summary>
public sealed class PilotTokenProvider(HttpClient httpClient, ISecretResolver secretResolver, IOptions<PilotOptions> options)
{
    private readonly PilotOptions _options = options.Value;
    private readonly ConcurrentDictionary<Guid, (string Token, DateTimeOffset ExpiresAt)> _cache = new();

    public async Task<string> GetTokenAsync(PosConnection connection, CancellationToken ct)
    {
        if (_cache.TryGetValue(connection.Id, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return cached.Token;
        }

        var apiKey = await secretResolver.ResolveAsync(connection.SecretRef, ct);
        var response = await httpClient.PostAsJsonAsync(
            $"{_options.BaseUrl}/Authorization/Token",
            new TokenRequest { ApiKey = apiKey },
            ct);
        response.EnsureSuccessStatusCode();

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(ct)
            ?? throw new InvalidOperationException("Pilot token endpoint returned an empty response.");

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(token.Exp) - _options.TokenRefreshMargin;
        _cache[connection.Id] = (token.Token, expiresAt);
        return token.Token;
    }
}
