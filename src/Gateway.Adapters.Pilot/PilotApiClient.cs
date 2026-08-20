using System.Net.Http.Headers;
using System.Net.Http.Json;
using Gateway.Adapters.Pilot.Dtos;
using Gateway.Domain.Tenancy;
using Microsoft.Extensions.Options;

namespace Gateway.Adapters.Pilot;

public sealed class PilotApiClient(HttpClient httpClient, PilotTokenProvider tokenProvider, IOptions<PilotOptions> options)
{
    private readonly PilotOptions _options = options.Value;

    public async Task<StatusResponse> CreateOnlineOrderAsync(PosConnection connection, OnlineOrderRequest order, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/OnlineOrder/Create")
        {
            Content = JsonContent.Create(order)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokenProvider.GetTokenAsync(connection, ct));

        var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StatusResponse>(ct) ?? new StatusResponse { Status = true };
    }

    public async Task<List<PilotMenuProduct>> GetMenuAsync(PosConnection connection, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_options.BaseUrl}/SalesProducts/Menu");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokenProvider.GetTokenAsync(connection, ct));

        var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<PilotMenuProduct>>(ct) ?? [];
    }

    public async Task<bool> CheckHealthAsync(PosConnection connection, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_options.BaseUrl}/api/Health/Check");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokenProvider.GetTokenAsync(connection, ct));

        var response = await httpClient.SendAsync(request, ct);
        return response.IsSuccessStatusCode;
    }
}
