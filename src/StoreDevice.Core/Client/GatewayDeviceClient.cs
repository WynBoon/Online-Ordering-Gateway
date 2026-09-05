using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Gateway.Domain.Enums;

namespace StoreDevice.Client;

public sealed class GatewayDeviceClient(HttpClient http, IDeviceSessionStore sessions)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task<DeviceSession> EnrollAsync(string baseUrl, string code, string name, CancellationToken ct = default)
    {
        var root = baseUrl.TrimEnd('/');
        var body = new
        {
            enrollmentCode = code,
            deviceName = name,
            hardwareFingerprint = Environment.MachineName
        };

        using var response = await http.PostAsJsonAsync($"{root}/device/enroll", body, Json, ct);
        var claimed = await ReadAsync<ClaimedEnrollmentDto>(response, ct);

        var session = new DeviceSession
        {
            GatewayBaseUrl = root,
            DeviceToken = claimed.DeviceToken,
            DeviceId = claimed.DeviceId,
            StoreId = claimed.StoreId,
            StoreName = claimed.StoreName,
            DeviceName = claimed.DeviceName,
            Functions = claimed.Functions
        };
        await sessions.SaveAsync(session, ct);
        return session;
    }

    public Task HeartbeatAsync(CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, "/device/heartbeat", null, ct);

    public Task<DeviceSessionDto> MeAsync(CancellationToken ct = default) =>
        GetAsync<DeviceSessionDto>("/device/me", ct);

    public Task<IReadOnlyList<DeviceOrderDto>> ListOrdersAsync(CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<DeviceOrderDto>>("/device/orders", ct);

    public Task MarkPreparingAsync(string orderRef, CancellationToken ct = default) =>
        PostActionAsync(orderRef, new { action = "preparing" }, ct);

    public Task MarkReadyAsync(string orderRef, CancellationToken ct = default) =>
        PostActionAsync(orderRef, new { action = "ready" }, ct);

    public Task MarkCompletedAsync(string orderRef, CancellationToken ct = default) =>
        PostActionAsync(orderRef, new { action = "completed" }, ct);

    public Task CancelAsync(string orderRef, CancelReason reason, CancellationToken ct = default) =>
        PostActionAsync(orderRef, new { action = "cancel", cancelReason = reason.ToString() }, ct);

    public Task DelayAsync(string orderRef, int minutes, CancellationToken ct = default) =>
        PostActionAsync(orderRef, new { action = "delay", delayMinutes = minutes }, ct);

    private Task PostActionAsync(string orderRef, object body, CancellationToken ct) =>
        SendAsync(HttpMethod.Post, $"/device/orders/{Uri.EscapeDataString(orderRef)}/actions", body, ct);

    private async Task<T> GetAsync<T>(string path, CancellationToken ct)
    {
        using var request = await AuthenticatedAsync(HttpMethod.Get, path, null, ct);
        using var response = await http.SendAsync(request, ct);
        return await ReadAsync<T>(response, ct);
    }

    private async Task SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var request = await AuthenticatedAsync(method, path, body, ct);
        using var response = await http.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
    }

    private async Task<HttpRequestMessage> AuthenticatedAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        var session = await sessions.GetAsync(ct)
            ?? throw new InvalidOperationException("This tablet is not paired.");
        var request = new HttpRequestMessage(method, session.GatewayBaseUrl.TrimEnd('/') + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.DeviceToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        return request;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        await EnsureSuccessAsync(response, ct);
        var value = await response.Content.ReadFromJsonAsync<T>(Json, ct);
        return value ?? throw new InvalidOperationException($"Gateway returned {response.StatusCode}");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string? message = null;
        try
        {
            var payload = await response.Content.ReadFromJsonAsync<ErrorBody>(Json, ct);
            message = payload?.Error;
        }
        catch (JsonException)
        {
            // fall through to status-code fallback
        }

        throw new InvalidOperationException(message ?? $"Gateway returned {response.StatusCode}");
    }
}
