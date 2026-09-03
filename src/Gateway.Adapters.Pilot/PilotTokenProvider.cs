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
        var token = await ExchangeAsync(apiKey, ct);
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(token.Exp) - _options.TokenRefreshMargin;
        _cache[connection.Id] = (token.Token, expiresAt);
        return token.Token;
    }

    /// <summary>
    /// Onboarding/test path: exchange a pasted (or stored) API key and return vendor,
    /// store, and permissions. Does not cache or return the JWT.
    /// </summary>
    public async Task<PilotConnectionProbe> ProbeApiKeyAsync(string apiKey, CancellationToken ct)
    {
        var token = await ExchangeAsync(apiKey, ct);
        if (string.IsNullOrWhiteSpace(token.VendorId) || string.IsNullOrWhiteSpace(token.StoreId))
        {
            throw new InvalidOperationException("Pilot token response did not include VendorId/StoreId.");
        }

        return new PilotConnectionProbe
        {
            VendorId = token.VendorId,
            StoreId = token.StoreId,
            Permissions = token.Permissions ?? [],
            TokenType = token.TokenType,
            ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(token.Exp)
        };
    }

    public void Invalidate(Guid connectionId) => _cache.TryRemove(connectionId, out _);

    private async Task<TokenResponse> ExchangeAsync(string apiKey, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        var response = await httpClient.PostAsJsonAsync(
            $"{_options.BaseUrl.TrimEnd('/')}/Authorization/Token",
            new TokenRequest { ApiKey = apiKey.Trim() },
            ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Pilot token endpoint returned {(int)response.StatusCode}: {body}",
                inner: null,
                statusCode: response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<TokenResponse>(ct)
            ?? throw new InvalidOperationException("Pilot token endpoint returned an empty response.");
    }
}
