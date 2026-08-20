using System.Net.Http.Json;
using Gateway.Adapters.Gaap.Dtos;
using Gateway.Application.Ports;
using Gateway.Domain.Tenancy;
using Microsoft.Extensions.Options;

namespace Gateway.Adapters.Gaap;

/// <summary>
/// Thin wrapper over the GAAP Unity Data-API. Auth is an <c>apikey</c> query
/// parameter scoped to the whole GAAP instance (ARCHITECTURE.md §2) — resolved
/// per call from <see cref="PosConnection.SecretRef"/>, never cached across
/// stores, since one apikey may cover an entire merchant's estate.
/// </summary>
public sealed class GaapApiClient(HttpClient httpClient, ISecretResolver secretResolver, IOptions<GaapOptions> options)
{
    private readonly GaapOptions _options = options.Value;

    public async Task<string> CreateSaleAsync(PosConnection connection, NewSalePayload payload, CancellationToken ct)
    {
        var apikey = await secretResolver.ResolveAsync(connection.SecretRef, ct);
        var response = await httpClient.PostAsJsonAsync($"{_options.BaseUrl}/sales/create?apikey={Uri.EscapeDataString(apikey)}", payload, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    public async Task<GaapProductRecordsResponse> GetProductsAsync(PosConnection connection, int limit, int skip, CancellationToken ct)
    {
        var apikey = await secretResolver.ResolveAsync(connection.SecretRef, ct);
        var url = $"{_options.BaseUrl}/products?apikey={Uri.EscapeDataString(apikey)}&limit={limit}&skip={skip}";
        var result = await httpClient.GetFromJsonAsync<GaapProductRecordsResponse>(url, ct);
        return result ?? new GaapProductRecordsResponse();
    }

    public async Task<GaapProductGroupsResponse> GetGroupsAsync(PosConnection connection, int limit, int skip, CancellationToken ct)
    {
        var apikey = await secretResolver.ResolveAsync(connection.SecretRef, ct);
        var url = $"{_options.BaseUrl}/groups?apikey={Uri.EscapeDataString(apikey)}&limit={limit}&skip={skip}";
        var result = await httpClient.GetFromJsonAsync<GaapProductGroupsResponse>(url, ct);
        return result ?? new GaapProductGroupsResponse();
    }

    /// <summary>Used both as the connectivity probe for GET /health (bounded to 1 record,
    /// since GAAP has no dedicated health endpoint — ARCHITECTURE.md §2) and by the
    /// status synthesizer to confirm a sale actually closed.</summary>
    public async Task<GaapSalesResponse> FindSaleByInvoiceNumberAsync(PosConnection connection, string invoiceNumber, CancellationToken ct)
    {
        var apikey = await secretResolver.ResolveAsync(connection.SecretRef, ct);
        var match = Uri.EscapeDataString($"[(\"invoiceNumber\",\"eq\",\"{invoiceNumber}\")]");
        var url = $"{_options.BaseUrl}/sales?apikey={Uri.EscapeDataString(apikey)}&match={match}&limit=1";
        var result = await httpClient.GetFromJsonAsync<GaapSalesResponse>(url, ct);
        return result ?? new GaapSalesResponse();
    }
}
