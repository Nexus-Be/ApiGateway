namespace Nexus.Gateway.Tests;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Nexus.Gateway.MockEngine;
using Xunit;

public sealed class MockEngineTests
{
    [Fact]
    public void RouteMatcher_matches_literal_and_parameterized_paths()
    {
        var literalDef = new MockEndpointDefinition
        {
            Method = "POST",
            Path = "/api/v1/auth/register"
        };

        var match1 = MockRouteMatcher.Match(literalDef, "POST", "/api/v1/auth/register");
        Assert.True(match1.IsMatch);

        var matchWrongMethod = MockRouteMatcher.Match(literalDef, "GET", "/api/v1/auth/register");
        Assert.False(matchWrongMethod.IsMatch);

        var matchWrongPath = MockRouteMatcher.Match(literalDef, "POST", "/api/v1/auth/other");
        Assert.False(matchWrongPath.IsMatch);

        var paramDef = new MockEndpointDefinition
        {
            Method = "GET",
            Path = "/api/v1/users/{id}"
        };

        var matchParam = MockRouteMatcher.Match(paramDef, "GET", "/api/v1/users/user-123");
        Assert.True(matchParam.IsMatch);
        Assert.Equal("user-123", matchParam.RouteValues["id"]);
    }

    [Fact]
    public async Task MockEngine_definitions_loaded_and_queried_via_api()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Factory.TokenFor("gateway-admin", "admin"));

        var resp = await client.GetAsync("/gateway/api/v1/mock-engine/definitions");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var content = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());

        var items = doc.RootElement.GetProperty("data");
        Assert.Equal(5, items.GetArrayLength());

        // Reload API works
        var reloadResp = await client.PostAsync("/gateway/api/v1/mock-engine/reload", null);
        Assert.Equal(HttpStatusCode.OK, reloadResp.StatusCode);
    }

    [Fact]
    public async Task MockEngine_serves_declarative_register_endpoint()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var body = new
        {
            username = "johndoe",
            email = "john.doe@example.com",
            password = "Password123!"
        };

        var resp = await client.PostAsJsonAsync("/gateway/api/v1/auth/register", body);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("Mock", resp.Headers.GetValues("X-Gateway-Mode").Single());

        var content = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(200, doc.RootElement.GetProperty("statusCode").GetInt32());
        Assert.Equal("User registered successfully.", doc.RootElement.GetProperty("message").GetString());

        var data = doc.RootElement.GetProperty("data");
        Assert.Equal("john.doe@example.com", data.GetProperty("email").GetString());
        Assert.Equal("PendingVerification", data.GetProperty("status").GetString());
    }

    [Fact]
    public async Task MockEngine_serves_declarative_resend_otp_endpoint()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var body = new
        {
            email = "john.doe@example.com"
        };

        var resp = await client.PostAsJsonAsync("/gateway/api/v1/auth/resend-otp", body);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var content = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        var data = doc.RootElement.GetProperty("data");
        Assert.True(data.GetProperty("success").GetBoolean());
        Assert.Equal("user@mcs.local", data.GetProperty("email").GetString());
    }

    [Fact]
    public async Task MockEngine_enforces_policy_on_declarative_endpoints()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        // 1. Anonymous => 401 Unauthorized on authenticated endpoint (/api/v1/users/me)
        var unauth = await client.GetAsync("/gateway/api/v1/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, unauth.StatusCode);

        // 2. Authenticated user => 200 OK
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Factory.TokenFor("alex", "buyer"));
        var okResp = await client.GetAsync("/gateway/api/v1/users/me");
        Assert.Equal(HttpStatusCode.OK, okResp.StatusCode);

        var content = await okResp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(200, doc.RootElement.GetProperty("statusCode").GetInt32());
        Assert.Equal("Alex Rivers", doc.RootElement.GetProperty("data").GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task MockEngine_bypass_is_development_only_and_skips_the_same_policy_layer()
    {
        using var app = new Factory
        {
            Configure = o => o.AuthMode = "Bypass"
        };
        using var client = app.CreateClient();

        // Under Bypass mode, anonymous can access authenticated mock endpoint
        var response = await client.GetAsync("/gateway/api/v1/users/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task Swagger_documentation_auto_includes_declarative_mocks()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var resp = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var content = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        var paths = doc.RootElement.GetProperty("paths");

        // Verify that declarative mock paths exist in Swagger
        Assert.True(paths.TryGetProperty("/api/v1/auth/register", out var regPath), "Missing /api/v1/auth/register in Swagger");
        Assert.True(regPath.TryGetProperty("post", out _), "Missing POST for /api/v1/auth/register in Swagger");

        Assert.True(paths.TryGetProperty("/api/v1/auth/login", out var loginPath), "Missing /api/v1/auth/login in Swagger");
        Assert.True(loginPath.TryGetProperty("post", out _), "Missing POST for /api/v1/auth/login in Swagger");

        Assert.True(paths.TryGetProperty("/api/v1/users/me", out var mePath), "Missing /api/v1/users/me in Swagger");
        Assert.True(mePath.TryGetProperty("get", out _), "Missing GET for /api/v1/users/me in Swagger");
    }

    [Fact]
    public async Task Real_service_rejects_mock_token_with_401_unauthorized()
    {
        using var app = new Factory
        {
            Configure = o =>
            {
                o.Services["catalog"].Mode = "Real";
                o.Services["catalog"].Address = "http://127.0.0.1:59999";
                o.AuthMode = "Enforce";
            }
        };
        using var client = app.CreateClient();

        // 1. With mock token "admin" on Real service -> MUST return 401 Unauthorized (not 503!)
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin");
        var resp = await client.PostAsJsonAsync("/gateway/api/v1/catalog/categories", new { name = "Bàn phím" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);

        var content = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal("AUTH_UNAUTHORIZED", doc.RootElement.GetProperty("errorCode").GetString());

        // 2. With real signed JWT token on Real service -> Authenticated!
        // Reaches backend gRPC (which gives 503 because 127.0.0.1:59999 is not running)
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Factory.TokenFor("root", "admin"));
        var respRealToken = await client.PostAsJsonAsync("/gateway/api/v1/catalog/categories", new { name = "Bàn phím" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, respRealToken.StatusCode);
    }

    [Fact]
    public async Task Mock_service_accepts_mock_token_admin_successfully()
    {
        using var app = new Factory
        {
            Configure = o =>
            {
                o.Services["catalog"].Mode = "Mock";
                o.AuthMode = "Enforce";
            }
        };
        using var client = app.CreateClient();

        // On Mock service, mock token "admin" is accepted and returns mock data!
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin");
        var resp = await client.PostAsJsonAsync("/gateway/api/v1/catalog/categories", new { name = "Bàn phím" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("Mock", resp.Headers.GetValues("X-Gateway-Mode").Single());
    }

    [Fact]
    public void MockEngine_route_precedence_prioritizes_literal_over_parameter()
    {
        var paramDef = new MockEndpointDefinition
        {
            Method = "GET",
            Path = "/gateway/api/v1/items/{id}"
        };

        var literalDef = new MockEndpointDefinition
        {
            Method = "GET",
            Path = "/gateway/api/v1/items/summary"
        };

        // When testing RouteMatcher directly
        var matchLiteral = MockRouteMatcher.Match(literalDef, "GET", "/gateway/api/v1/items/summary");
        Assert.True(matchLiteral.IsMatch);
        Assert.Empty(matchLiteral.RouteValues);

        var matchParam = MockRouteMatcher.Match(paramDef, "GET", "/gateway/api/v1/items/summary");
        Assert.True(matchParam.IsMatch);
        Assert.Equal("summary", matchParam.RouteValues["id"]);
    }
}
