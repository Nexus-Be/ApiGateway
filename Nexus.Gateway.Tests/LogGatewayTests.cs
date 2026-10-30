namespace Nexus.Gateway.Tests;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

public sealed class LogGatewayTests
{
    private static HttpClient CreateClient(Factory app, string role)
    {
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor("log-admin", role));
        return client;
    }

    [Fact]
    public async Task QueryLogs_Staff_ReturnsPagedEnvelope()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "staff");

        using var response = await client.GetAsync("/api/v1/log/logs?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Mock", response.Headers.GetValues("X-Gateway-Mode").Single());

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        var data = root.GetProperty("data");
        Assert.True(data.GetProperty("items").GetArrayLength() > 0);
        Assert.True(data.GetProperty("pagination").GetProperty("totalCount").GetInt32() > 0);
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=101")]
    public async Task QueryLogs_InvalidPagination_Returns400(string query)
    {
        using var app = new Factory();
        using var client = CreateClient(app, "staff");

        using var response = await client.GetAsync("/api/v1/log/logs" + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("VAL_INVALID_INPUT", doc.RootElement.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task QueryLogs_Buyer_ReturnsForbidden()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "buyer");

        using var response = await client.GetAsync("/api/v1/log/logs");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetLog_ValidId_ReturnsEntry()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "staff");

        using var response = await client.GetAsync("/api/v1/log/logs/a1b2c3d4-1111-4444-8888-123456789001");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = doc.RootElement.GetProperty("data");
        Assert.True(data.TryGetProperty("eventType", out _));
        Assert.True(data.TryGetProperty("serviceName", out _));
    }

    [Fact]
    public async Task GetLog_InvalidGuid_Returns400()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "staff");

        using var response = await client.GetAsync("/api/v1/log/logs/not-a-guid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("VAL_INVALID_INPUT", doc.RootElement.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task ListLogEventTypes_Staff_ReturnsItems()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "staff");

        using var response = await client.GetAsync("/api/v1/log/event-types");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = doc.RootElement.GetProperty("data").GetProperty("items");
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
        Assert.True(items.GetArrayLength() > 0);
    }

    [Fact]
    public async Task SetLogEventTypeEnabled_Admin_ReturnsUpdatedType()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "admin");

        using var response = await client.PutAsJsonAsync(
            "/api/v1/log/event-types/ORDER_CREATED", new { isEnabled = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = doc.RootElement.GetProperty("data");
        Assert.Equal("ORDER_CREATED", data.GetProperty("code").GetString());
    }

    [Fact]
    public async Task SetLogEventTypeEnabled_Staff_ReturnsForbidden()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "staff");

        using var response = await client.PutAsJsonAsync(
            "/api/v1/log/event-types/ORDER_CREATED", new { isEnabled = false });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task BatchLog_AcceptsPayloadAndReturns202()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/log/batch", new[] { new { level = "Information", message = "test batch entry" } });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ACCEPTED", doc.RootElement.GetProperty("data").GetProperty("status").GetString());
    }
}
