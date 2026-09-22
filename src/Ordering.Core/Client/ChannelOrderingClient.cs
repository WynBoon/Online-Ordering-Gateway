using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Ordering.Cart;

namespace Ordering.Client;

public sealed class ChannelOrderingClient(HttpClient http, IOrderingSessionStore sessions) : IOrderingClient
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const int MenuTimeoutSeconds = 30;
    private const int InjectTimeoutSeconds = 10;
    private const int DefaultTimeoutSeconds = 15;
    private const int RetryableAttempts = 3;

    public async Task<HealthResult> GetHealthAsync(CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Get, "/health", null, DefaultTimeoutSeconds, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw new ChannelException("Unknown location key.");
        }

        var dto = await ReadOptionalAsync<HealthDto>(response, ct);
        var ok = response.IsSuccessStatusCode &&
                 string.Equals(dto?.Status, "ok", StringComparison.OrdinalIgnoreCase);
        return new HealthResult(ok, dto?.Detail ?? dto?.Status);
    }

    public async Task<MenuDto> GetMenuAsync(CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Get, "/menu", null, MenuTimeoutSeconds, ct);
        await EnsureSuccessAsync(response, ct);
        return await ReadAsync<MenuDto>(response, ct);
    }

    public async Task<ChannelOrderDto> GetOrderAsync(string orderRef, CancellationToken ct = default)
    {
        using var response = await SendAsync(
            HttpMethod.Get,
            $"/orders/{Uri.EscapeDataString(orderRef)}",
            null,
            DefaultTimeoutSeconds,
            ct);
        if (response.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.MethodNotAllowed)
        {
            var envelope = await ReadOptionalAsync<ErrorEnvelope>(response, ct);
            throw new ChannelException(
                envelope?.Message
                ?? "This API does not yet expose GET /orders, so live status cannot load. The order was still placed.");
        }

        await EnsureSuccessAsync(response, ct);
        return await ReadAsync<ChannelOrderDto>(response, ct);
    }

    public async Task<IReadOnlyList<ChannelOrderDto>> ListOrdersAsync(CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Get, "/orders", null, DefaultTimeoutSeconds, ct);
        await EnsureSuccessAsync(response, ct);
        var list = await ReadAsync<List<ChannelOrderDto>>(response, ct);
        return list;
    }

    public async Task<InjectionResult> PlaceOrderAsync(PlaceOrderCommand command, CancellationToken ct = default)
    {
        var session = await RequireSessionAsync(ct);
        var idempotencyKey = string.IsNullOrWhiteSpace(command.IdempotencyKey)
            ? Guid.NewGuid().ToString()
            : command.IdempotencyKey!;

        var body = BuildRequest(command, session);

        InjectionResult? last = null;
        for (var attempt = 1; attempt <= RetryableAttempts; attempt++)
        {
            last = await PlaceOnceAsync(command.OrderRef, command.DisplayId, idempotencyKey, body, ct);
            if (last.Success || !last.Retryable)
            {
                return last;
            }

            if (attempt < RetryableAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
        }

        return last!;
    }

    private async Task<InjectionResult> PlaceOnceAsync(
        string orderRef,
        string displayId,
        string idempotencyKey,
        OrderInjectionRequest body,
        CancellationToken ct)
    {
        using var request = await AuthenticatedAsync(HttpMethod.Post, "/orders", body, ct);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(InjectTimeoutSeconds));

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new InjectionResult(
                false, orderRef, displayId, idempotencyKey, null, 0, "timeout",
                "The gateway did not answer within 10s.", true);
        }

        using (response)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.Created)
            {
                var created = await ReadOptionalAsync<OrderInjectionResponse>(response, ct);
                return new InjectionResult(
                    true, orderRef, displayId, idempotencyKey, created?.PosOrderId,
                    (int)response.StatusCode, null, null, false);
            }

            var error = await ReadOptionalAsync<ErrorEnvelope>(response, ct);
            return new InjectionResult(
                false, orderRef, displayId, idempotencyKey, null,
                (int)response.StatusCode, error?.Code, error?.Message ?? $"Gateway returned {response.StatusCode}",
                error?.Retryable ?? ((int)response.StatusCode is 503 or 504));
        }
    }

    private static OrderInjectionRequest BuildRequest(PlaceOrderCommand command, OrderingSession session)
    {
        var items = command.Items.Select((line, index) => new OrderItemDto
        {
            ExternalProductId = index == 0 && !string.IsNullOrWhiteSpace(command.UnknownPluOverride)
                ? command.UnknownPluOverride!
                : line.ExternalProductId,
            Name = line.Name,
            Quantity = line.Quantity,
            UnitPriceCents = line.UnitPriceCents,
            TotalPriceCents = line.TotalPriceCents,
            Notes = line.Notes,
            Modifiers = line.Modifiers.Select(m => new OrderModifierDto
            {
                ExternalModifierId = m.ExternalModifierId,
                GroupExternalId = m.GroupExternalId,
                Name = m.Name,
                Quantity = m.Quantity,
                PriceDeltaCents = m.PriceDeltaCents
            }).ToList()
        }).ToList();

        return new OrderInjectionRequest
        {
            OrderRef = command.OrderRef,
            DisplayId = command.DisplayId,
            SourceChannel = session.SourceChannel,
            LocationId = session.LocationId,
            FulfillmentType = command.FulfillmentType,
            PlacedAt = DateTimeOffset.UtcNow,
            ScheduledFor = command.ScheduledFor,
            Customer = string.IsNullOrWhiteSpace(command.CustomerName)
                && string.IsNullOrWhiteSpace(command.CustomerPhone)
                && string.IsNullOrWhiteSpace(command.CustomerEmail)
                ? null
                : new CustomerDto
                {
                    Name = command.CustomerName,
                    Phone = command.CustomerPhone,
                    Email = command.CustomerEmail
                },
            DeliveryAddress = command.FulfillmentType == CartState.Delivery
                ? new DeliveryAddressDto
                {
                    Line1 = command.DeliveryLine1 ?? "",
                    City = command.DeliveryCity,
                    PostalCode = command.DeliveryPostalCode
                }
                : null,
            Items = items,
            SubtotalCents = command.SubtotalCents,
            TaxCents = command.TaxCents,
            DeliveryFeeCents = command.DeliveryFeeCents,
            TipCents = command.TipCents,
            TotalCents = command.TotalCents,
            Currency = command.Currency,
            Payment = new PaymentDto { Prepaid = true },
            Notes = command.Notes
        };
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        object? body,
        int timeoutSeconds,
        CancellationToken ct)
    {
        using var request = await AuthenticatedAsync(method, path, body, ct);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            return await http.SendAsync(request, cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ChannelException($"The gateway did not answer within {timeoutSeconds}s.");
        }
    }

    private async Task<HttpRequestMessage> AuthenticatedAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        var session = await RequireSessionAsync(ct);
        var request = new HttpRequestMessage(method, session.GatewayBaseUrl.TrimEnd('/') + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.LocationKey);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        return request;
    }

    private async Task<OrderingSession> RequireSessionAsync(CancellationToken ct) =>
        await sessions.GetAsync(ct)
        ?? throw new ChannelException("Connect to a store first.");

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(Json, ct);
        return value ?? throw new ChannelException($"Gateway returned {response.StatusCode} with an empty body.");
    }

    private static async Task<T?> ReadOptionalAsync<T>(HttpResponseMessage response, CancellationToken ct) where T : class
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(Json, ct);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var error = await ReadOptionalAsync<ErrorEnvelope>(response, ct);
        throw new ChannelException(error?.Message ?? $"Gateway returned {response.StatusCode}");
    }
}
