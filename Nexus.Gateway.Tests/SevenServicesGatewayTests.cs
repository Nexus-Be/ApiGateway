using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Nexus.Gateway.Tests;

public sealed class SevenServicesGatewayTests
{
    [Fact]
    public async Task Get_and_update_gateway_modes_runtime()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Factory.TokenFor("gateway-admin", "admin"));

        // 1. Get all modes
        var getResp = await client.GetAsync("/gateway/api/v1/gateway/modes");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var getContent = await getResp.Content.ReadAsStringAsync();
        using var getDoc = JsonDocument.Parse(getContent);

        Assert.True(getDoc.RootElement.GetProperty("success").GetBoolean());
        var data = getDoc.RootElement.GetProperty("data");

        Assert.Contains(data.GetProperty("gatewayMode").GetString(), new[] { "Standard", "Bypass" });
        var services = data.GetProperty("services");

        // Verify all 8 services exist
        foreach (var svc in new[] { "catalog", "commerce", "fulfillment", "users", "auction", "notification", "log", "wallet" })
        {
            Assert.True(services.TryGetProperty(svc, out var svcObj), $"Missing service in modes: {svc}");
            Assert.Equal("Mock", svcObj.GetProperty("mode").GetString());
        }

        // 2. Update users mode to Real
        var updateResp = await client.PutAsJsonAsync("/gateway/api/v1/gateway/modes/users", new { mode = "Real" });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);

        // 3. Verify users mode is now Real
        var checkResp = await client.GetAsync("/gateway/api/v1/gateway/modes");
        var checkContent = await checkResp.Content.ReadAsStringAsync();
        using var checkDoc = JsonDocument.Parse(checkContent);
        Assert.Equal("Real", checkDoc.RootElement.GetProperty("data").GetProperty("services").GetProperty("users").GetProperty("mode").GetString());

        // 4. Reset users back to Mock
        var resetResp = await client.PutAsJsonAsync("/gateway/api/v1/gateway/modes/users", new { mode = "Mock" });
        Assert.Equal(HttpStatusCode.OK, resetResp.StatusCode);
    }

    [Fact]
    public async Task Gateway_control_plane_requires_a_real_administrator_even_when_bypass_is_enabled()
    {
        using var app = new Factory
        {
            Configure = o => o.AuthMode = "Bypass"
        };
        using var client = app.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/gateway/api/v1/gateway/modes")).StatusCode);

        client.DefaultRequestHeaders.Authorization = new("Bearer", "admin");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/gateway/api/v1/gateway/modes")).StatusCode);

        client.DefaultRequestHeaders.Authorization = new("Bearer", Factory.TokenFor("gateway-admin", "admin"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/gateway/api/v1/gateway/modes")).StatusCode);
    }

    [Fact]
    public async Task Client_header_override_mode_per_request()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var loginBody = new { username = "buyer01", password = "Password123!" };

        // Default mock response has X-Gateway-Mode: Mock
        var defaultResp = await client.PostAsJsonAsync("/gateway/api/v1/auth/login", loginBody);
        Assert.Equal(HttpStatusCode.OK, defaultResp.StatusCode);
        Assert.Equal("Mock", defaultResp.Headers.GetValues("X-Gateway-Mode").Single());

        // With header X-Mock-Mode: Real -> Gateway executes Real flow (fails with ServiceUnavailable since no real gRPC is running)
        using var realReq = new HttpRequestMessage(HttpMethod.Post, "/gateway/api/v1/auth/login")
        {
            Content = JsonContent.Create(loginBody)
        };
        realReq.Headers.Add("X-Mock-Mode", "Real");
        var realResp = await client.SendAsync(realReq);
        Assert.Equal("Real", realResp.Headers.GetValues("X-Gateway-Mode").Single());
        Assert.Contains(realResp.StatusCode, new[] { HttpStatusCode.ServiceUnavailable, HttpStatusCode.BadGateway });

        // With X-Service-Modes: users=Mock;auction=Real
        using var mixedReq = new HttpRequestMessage(HttpMethod.Post, "/gateway/api/v1/auth/login")
        {
            Content = JsonContent.Create(loginBody)
        };
        mixedReq.Headers.Add("X-Service-Modes", "users=Mock;auction=Real");
        var mixedResp = await client.SendAsync(mixedReq);
        Assert.Equal("Mock", mixedResp.Headers.GetValues("X-Gateway-Mode").Single());
        Assert.Equal(HttpStatusCode.OK, mixedResp.StatusCode);
    }

    [Fact]
    public async Task Bypass_auth_allows_calling_authenticated_endpoint_in_development()
    {
        using var app = new Factory
        {
            Configure = o => o.AuthMode = "Bypass"
        };
        using var client = app.CreateClient();

        // In Bypass mode without any Authorization header, GET /me succeeds
        var resp = await client.GetAsync("/gateway/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task Rate_limiting_applies_regardless_of_mock_or_real_mode()
    {
        using var app = new Factory
        {
            Configure = o =>
            {
                o.PermitLimit = 3;
                o.RateLimits["auth-login"] = new() { PermitLimit = 3, WindowSeconds = 60 };
                o.WindowSeconds = 60;
                o.RedisFailOpenInDevelopment = false;
            }
        };
        using var client = app.CreateClient();

        var body = new { username = "buyer01", password = "Password123!" };

        // 3 calls within limit succeed
        for (int i = 0; i < 3; i++)
        {
            var okResp = await client.PostAsJsonAsync("/gateway/api/v1/auth/login", body);
            Assert.Equal(HttpStatusCode.OK, okResp.StatusCode);
        }

        // 4th call is rate limited -> 429 Too Many Requests
        var limitedResp = await client.PostAsJsonAsync("/gateway/api/v1/auth/login", body);
        Assert.Equal(HttpStatusCode.TooManyRequests, limitedResp.StatusCode);
        Assert.True(limitedResp.Headers.Contains("Retry-After"));
    }
}
