namespace Nexus.Gateway.Tests;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

public sealed class FulfillmentMockTests
{
    private const string UserId = "11111111-1111-1111-1111-000000000001";
    private const string ShipmentId = "ship-001";
    private const string OrderId = "ord-001";

    private static HttpClient CreateClient(Factory app, string role = "buyer")
    {
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor(UserId, role));
        return client;
    }

    [Fact]
    public async Task GetShipment_ReturnsSuccess()
    {
        using var app = new Factory();
        using var client = CreateClient(app);

        var response = await client.GetAsync($"/api/v1/shipments/{ShipmentId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Mock", response.Headers.GetValues("X-Gateway-Mode").Single());

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(200, root.GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task GetShipmentsByOrder_ReturnsSuccess()
    {
        using var app = new Factory();
        using var client = CreateClient(app);

        var response = await client.GetAsync($"/api/v1/shipments/order/{OrderId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ListSellerShipments_ReturnsSuccess()
    {
        using var app = new Factory();
        using var client = CreateClient(app, role: "seller");

        var response = await client.GetAsync("/api/v1/seller/shipment?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task SearchSellerShipments_ReturnsSuccess()
    {
        using var app = new Factory();
        using var client = CreateClient(app, role: "seller");

        var response = await client.GetAsync("/api/v1/seller/shipment/search?page=1&pageSize=10&keyword=GHN");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSellerShipmentById_ReturnsSuccess()
    {
        using var app = new Factory();
        using var client = CreateClient(app, role: "seller");

        var response = await client.GetAsync($"/api/v1/seller/shipment/{ShipmentId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTracking_ReturnsSuccess()
    {
        using var app = new Factory();
        using var client = CreateClient(app);

        var response = await client.PatchAsJsonAsync($"/api/v1/shipments/{ShipmentId}/tracking", new
        {
            trackingNumber = "GHN-123456",
            status = "IN_TRANSIT",
            location = "Ha Noi SOC"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CompleteShipment_ReturnsSuccess()
    {
        using var app = new Factory();
        using var client = CreateClient(app);

        var response = await client.PostAsync($"/api/v1/shipment/{ShipmentId}/complete", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CarrierWebhook_Public_ReturnsSuccess()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/shipments/webhook", new
        {
            trackingNumber = "GHN-123456",
            status = "DELIVERED",
            carrier = "GHN"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
