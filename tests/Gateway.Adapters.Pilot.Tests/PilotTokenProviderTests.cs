using System.Net;
using System.Text;
using System.Text.Json;
using Gateway.Adapters.Pilot.Dtos;
using Gateway.Application.Ports;
using Gateway.Domain.Enums;
using Gateway.Domain.Tenancy;
using Microsoft.Extensions.Options;
using Moq;

namespace Gateway.Adapters.Pilot.Tests;

public class PilotTokenResponseTests
{
    [Fact]
    public void Deserializes_vendor_store_and_permissions()
    {
        const string json = """
            {
                "Token": "eyJ",
                "TokenType": "Bearer",
                "VendorId": "715",
                "StoreId": "13305",
                "Permissions": ["OnlineOrders", "SalesProducts"],
                "nbf": 1788155325,
                "exp": 1788241725
            }
            """;

        var token = JsonSerializer.Deserialize<TokenResponse>(json);

        Assert.NotNull(token);
        Assert.Equal("Bearer", token.TokenType);
        Assert.Equal("715", token.VendorId);
        Assert.Equal("13305", token.StoreId);
        Assert.Equal(["OnlineOrders", "SalesProducts"], token.Permissions);
        Assert.Equal(1788241725, token.Exp);
    }
}

public class PilotTokenProviderTests
{
    [Fact]
    public async Task Probe_returns_identity_and_permissions_not_the_jwt()
    {
        var handler = new StubHandler
        {
            ResponseJson = """
                {
                    "Token": "secret-jwt",
                    "TokenType": "Bearer",
                    "VendorId": "715",
                    "StoreId": "13305",
                    "Permissions": ["OnlineOrders", "StoreStatusRead"],
                    "exp": 1788241725
                }
                """
        };
        var provider = new PilotTokenProvider(
            new HttpClient(handler),
            Mock.Of<ISecretResolver>(),
            Options.Create(new PilotOptions { BaseUrl = "https://pilot-qa.test" }));

        var probe = await provider.ProbeApiKeyAsync("the-api-key", CancellationToken.None);

        Assert.Equal("715", probe.VendorId);
        Assert.Equal("13305", probe.StoreId);
        Assert.True(probe.HasOnlineOrders);
        Assert.Equal("Bearer", probe.TokenType);
        Assert.Contains("/Authorization/Token", handler.LastRequest?.RequestUri?.ToString());
        var body = await handler.LastRequest!.Content!.ReadAsStringAsync();
        Assert.Contains("the-api-key", body);
    }

    [Fact]
    public async Task Probe_fails_when_token_omits_store_identity()
    {
        var handler = new StubHandler
        {
            ResponseJson = """{ "Token": "jwt", "VendorId": "715", "exp": 1 }"""
        };
        var provider = new PilotTokenProvider(
            new HttpClient(handler),
            Mock.Of<ISecretResolver>(),
            Options.Create(new PilotOptions { BaseUrl = "https://pilot-qa.test" }));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.ProbeApiKeyAsync("the-api-key", CancellationToken.None));
    }

    [Fact]
    public async Task Probe_surfaces_non_success_status()
    {
        var handler = new StubHandler { Status = HttpStatusCode.Unauthorized, ResponseJson = "bad key" };
        var provider = new PilotTokenProvider(
            new HttpClient(handler),
            Mock.Of<ISecretResolver>(),
            Options.Create(new PilotOptions { BaseUrl = "https://pilot-qa.test" }));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            provider.ProbeApiKeyAsync("the-api-key", CancellationToken.None));
        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string ResponseJson { get; init; } = "{}";
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(Status)
            {
                Content = new StringContent(ResponseJson, Encoding.UTF8, "application/json")
            });
        }
    }
}

public class PilotPosConfigTests
{
    [Fact]
    public void ApplyProbe_writes_vendor_site_key_and_permission_snapshot()
    {
        var connection = new PosConnection { StoreId = Guid.NewGuid(), SecretRef = "old" };
        var probe = new PilotConnectionProbe
        {
            VendorId = "715",
            StoreId = "13305",
            Permissions = ["OnlineOrders", "SalesProducts"],
            TokenType = "Bearer",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };

        PilotPosConfig.ApplyProbe(connection, probe, "new-api-key");

        Assert.Equal(PosType.Pilot, connection.PosType);
        Assert.Equal("new-api-key", connection.SecretRef);
        Assert.Equal("715", connection.ExternalNodeId);
        Assert.Equal("13305", connection.ExternalLocationId);
        Assert.Equal(["OnlineOrders", "SalesProducts"], PilotPosConfig.ReadPermissions(connection));
        Assert.NotNull(PilotPosConfig.ReadProbedAt(connection));
    }

    [Fact]
    public void ApplyProbe_keeps_stored_key_when_no_new_key_is_pasted()
    {
        var connection = new PosConnection { StoreId = Guid.NewGuid(), SecretRef = "stored-key" };
        var probe = new PilotConnectionProbe
        {
            VendorId = "1",
            StoreId = "2",
            Permissions = [],
            ExpiresAt = DateTimeOffset.UtcNow
        };

        PilotPosConfig.ApplyProbe(connection, probe, apiKey: null);

        Assert.Equal("stored-key", connection.SecretRef);
    }
}
