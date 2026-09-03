using System.Net;
using System.Text;
using System.Text.Json;
using Gateway.Adapters.Pilot.Dtos;
using Gateway.Application.Ports;
using Gateway.Domain.Tenancy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Gateway.Adapters.Pilot.Tests;

public class PilotMenuAdapterTests
{
    [Fact]
    public void Menu_response_object_deserializes_plu_items_not_a_raw_array()
    {
        const string json = """
            {
              "storeId": "13305",
              "status": true,
              "message": "OK",
              "PluItems": [
                {
                  "Plu": "1001",
                  "ItemName": "Test burger",
                  "Price": 85.00,
                  "Dtab": "MAINS",
                  "Options": [
                    {
                      "OptionName": "Extras",
                      "OptionItems": [
                        { "Plu": "2001", "ItemName": "Bacon", "Price": 12.50 }
                      ]
                    }
                  ]
                }
              ]
            }
            """;

        var menu = JsonSerializer.Deserialize<PilotMenuResponse>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(menu);
        Assert.Equal("13305", menu.StoreId);
        var item = Assert.Single(menu.PluItems!);
        Assert.Equal("1001", item.Plu);
        Assert.Equal("Test burger", item.ItemName);
        Assert.Equal(85, item.Price);
        Assert.Equal("MAINS", item.Dtab);
        Assert.Equal("2001", item.Options![0].OptionItems![0].Plu);
    }

    [Fact]
    public void Root_array_is_not_a_menu_response()
    {
        const string json = """[{ "plu": "1", "description": "x", "price": 1 }]""";

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<PilotMenuResponse>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public async Task Maps_plu_items_into_categories_and_converts_rands_to_cents()
    {
        var connection = new PosConnection { StoreId = Guid.NewGuid(), SecretRef = "k" };
        var client = new PilotApiClient(
            new HttpClient(new StubHandler
            {
                ResponseJson = """
                    {
                      "status": true,
                      "PluItems": [
                        { "Plu": "1001", "ItemName": "Burger", "Price": 85.5, "Dtab": "MAINS" },
                        { "Plu": "1002", "ItemName": "Coke", "Price": 20, "Dtab": "DRINKS" }
                      ]
                    }
                    """
            }),
            new PilotTokenProvider(
                new HttpClient(new StubHandler { ResponseJson = """{"Token":"t","exp":9999999999}""" }),
                Mock.Of<ISecretResolver>(s => s.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()) == Task.FromResult("key")),
                Options.Create(new PilotOptions { BaseUrl = "https://pilot-qa.test" })),
            Options.Create(new PilotOptions { BaseUrl = "https://pilot-qa.test" }),
            NullLogger<PilotApiClient>.Instance);

        var menu = await new PilotMenuAdapter(client).GetMenuAsync(connection, CancellationToken.None);

        Assert.Equal(connection.StoreId, menu.StoreId);
        Assert.Equal(2, menu.Categories.Count);
        var burger = menu.Categories.Single(c => c.Name == "MAINS").Products.Single();
        Assert.Equal("1001", burger.ExternalId);
        Assert.Equal(8550, burger.PriceCents);
    }

    [Fact]
    public void ToCents_rounds_away_from_zero()
    {
        Assert.Equal(8500, PilotMenuAdapter.ToCents(85));
        Assert.Equal(1250, PilotMenuAdapter.ToCents(12.5));
        Assert.Equal(1, PilotMenuAdapter.ToCents(0.005));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public string ResponseJson { get; init; } = "{}";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ResponseJson, Encoding.UTF8, "application/json")
            });
    }
}
