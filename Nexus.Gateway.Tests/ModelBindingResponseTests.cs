using System.Net;
using System.Text.Json;
using Xunit;

namespace Nexus.Gateway.Tests;

public sealed class ModelBindingResponseTests
{
    [Fact]
    public async Task Non_numeric_page_uses_standard_gateway_error_body()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        using var response = await client.GetAsync("/api/v1/catalog/products?page=not-a-number");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("VAL_INVALID_INPUT", body.RootElement.GetProperty("errorCode").GetString());
        Assert.Equal(400, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(response.Headers.GetValues("X-Trace-Id").Single(), body.RootElement.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task Malformed_json_uses_standard_gateway_error_body()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = new StringContent("{invalid", System.Text.Encoding.UTF8, "application/json")
        };
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("VAL_INVALID_INPUT", body.RootElement.GetProperty("errorCode").GetString());
        Assert.Equal(response.Headers.GetValues("X-Trace-Id").Single(), body.RootElement.GetProperty("traceId").GetString());
    }
}
