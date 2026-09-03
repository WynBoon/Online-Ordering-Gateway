using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Gateway.Adapters.Pilot.Dtos;
using Gateway.Domain.Tenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gateway.Adapters.Pilot;

public sealed class PilotApiClient(
    HttpClient httpClient,
    PilotTokenProvider tokenProvider,
    IOptions<PilotOptions> options,
    ILogger<PilotApiClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly PilotOptions _options = options.Value;

    public async Task<StatusResponse> CreateOnlineOrderAsync(PosConnection connection, OnlineOrderRequest order, CancellationToken ct)
    {
        var url = $"{_options.BaseUrl.TrimEnd('/')}/OnlineOrder/Create";
        var payload = JsonSerializer.Serialize(order, JsonOptions);
        logger.LogInformation(
            "Pilot OnlineOrder/Create request for {OrderRef} vendor={VendorId} site={SiteId} orderId={OrderId} url={Url} body={Body}",
            order.OrderReference, order.VendorId, order.SiteId, order.OrderId, url, payload);

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokenProvider.GetTokenAsync(connection, ct));

        var response = await httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError(
                "Pilot OnlineOrder/Create failed {StatusCode} for {OrderRef}: {Body}",
                (int)response.StatusCode, order.OrderReference, body);
            throw new HttpRequestException(
                $"Pilot OnlineOrder/Create returned {(int)response.StatusCode}: {body}",
                inner: null,
                statusCode: response.StatusCode);
        }

        logger.LogInformation(
            "Pilot OnlineOrder/Create succeeded {StatusCode} for {OrderRef}: {Body}",
            (int)response.StatusCode, order.OrderReference, body);

        return JsonSerializer.Deserialize<StatusResponse>(body, JsonOptions) ?? new StatusResponse { Status = true };
    }

    public async Task<PilotMenuResponse> GetMenuAsync(PosConnection connection, CancellationToken ct)
    {
        var url = $"{_options.BaseUrl.TrimEnd('/')}/SalesProducts/Menu";
        logger.LogInformation("Pilot GET {Url} for store {StoreId}", url, connection.StoreId);

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokenProvider.GetTokenAsync(connection, ct));

        var response = await httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Pilot GET {Url} failed {StatusCode}: {Body}", url, (int)response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }

        logger.LogInformation("Pilot GET {Url} returned {StatusCode} ({Length} bytes)", url, (int)response.StatusCode, body.Length);
        return JsonSerializer.Deserialize<PilotMenuResponse>(body, JsonOptions) ?? new PilotMenuResponse();
    }

    public async Task<bool> CheckHealthAsync(PosConnection connection, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_options.BaseUrl.TrimEnd('/')}/api/Health/Check");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokenProvider.GetTokenAsync(connection, ct));

        var response = await httpClient.SendAsync(request, ct);
        logger.LogInformation("Pilot Health/Check returned {StatusCode} for store {StoreId}", (int)response.StatusCode, connection.StoreId);
        return response.IsSuccessStatusCode;
    }
}
