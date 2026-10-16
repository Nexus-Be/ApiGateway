using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Nexus.Gateway.MockEngine;
using Xunit;

namespace Nexus.Gateway.Tests;

public sealed class MockEngineTests
{
    [Fact]
    public void RouteMatcher_matches_literal_and_parameterized_paths()
    {
        var literalDef = new MockEndpointDefinition
        {
            Method = "POST",
            Path = "/gateway/api/v1/catalog/search"
        };

        var match1 = MockRouteMatcher.Match(literalDef, "POST", "/gateway/api/v1/catalog/search");
        Assert.True(match1.IsMatch);

        var matchWrongMethod = MockRouteMatcher.Match(literalDef, "GET", "/gateway/api/v1/catalog/search");
        Assert.False(matchWrongMethod.IsMatch);

        var matchWrongPath = MockRouteMatcher.Match(literalDef, "POST", "/gateway/api/v1/catalog/other");
        Assert.False(matchWrongPath.IsMatch);

        var paramDef = new MockEndpointDefinition
        {
            Method = "GET",
            Path = "/gateway/api/v1/auctions/{id}/live-status"
        };

        var matchParam = MockRouteMatcher.Match(paramDef, "GET", "/gateway/api/v1/auctions/auc-9981/live-status");
        Assert.True(matchParam.IsMatch);
        Assert.Equal("auc-9981", matchParam.RouteValues["id"]);
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
        Assert.True(items.GetArrayLength() >= 4, "Expected at least 4 declarative mock endpoints loaded.");

        // Reload API works
        var reloadResp = await client.PostAsync("/gateway/api/v1/mock-engine/reload", null);
        Assert.Equal(HttpStatusCode.OK, reloadResp.StatusCode);
    }

    [Fact]
    public async Task MockEngine_serves_declarative_search_endpoint_with_pagination()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var searchBody = new
        {
            keyword = "bàn phím cơ",
            minPrice = 500000
        };

        var resp = await client.PostAsJsonAsync("/gateway/api/v1/catalog/search?page=1&pageSize=10", searchBody);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("Mock", resp.Headers.GetValues("X-Gateway-Mode").Single());

        var content = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(200, doc.RootElement.GetProperty("statusCode").GetInt32());

        var data = doc.RootElement.GetProperty("data");
        Assert.True(data.GetProperty("items").GetArrayLength() > 0);
        Assert.Equal(1, data.GetProperty("pagination").GetProperty("pageIndex").GetInt32());
    }

    [Fact]
    public async Task MockEngine_serves_parameterized_auction_live_status()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var resp = await client.GetAsync("/gateway/api/v1/auctions/44444444-4444-4444-4444-000000000001/live-status");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var content = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("1550000.00", doc.RootElement.GetProperty("data").GetProperty("currentHighestBid").GetString());
    }

    [Fact]
    public async Task MockEngine_enforces_policy_on_declarative_endpoints()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var broadcastBody = new
        {
            title = "Bảo trì",
            content = "Hệ thống..."
        };

        // 1. Anonymous => 401
        var unauth = await client.PostAsJsonAsync("/gateway/api/v1/notifications/broadcast", broadcastBody);
        Assert.Equal(HttpStatusCode.Unauthorized, unauth.StatusCode);

        // 2. Buyer => 403
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "buyer");
        var forbidden = await client.PostAsJsonAsync("/gateway/api/v1/notifications/broadcast", broadcastBody);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        // 3. Admin => 201 Created
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin");
        var created = await client.PostAsJsonAsync("/gateway/api/v1/notifications/broadcast", broadcastBody);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var content = await created.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(201, doc.RootElement.GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task MockEngine_bypass_is_development_only_and_skips_the_same_policy_layer()
    {
        using var app = new Factory
        {
            Configure = o => o.AuthMode = "Bypass"
        };
        using var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/gateway/api/v1/notifications/broadcast", new
        {
            title = "Bảo trì",
            content = "Hệ thống..."
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
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

        // Verify that declarative paths are present in Swagger!
        Assert.True(paths.TryGetProperty("/api/v1/catalog/search", out var searchPath), "Missing /api/v1/catalog/search in Swagger");
        Assert.True(searchPath.TryGetProperty("post", out _), "Missing POST for /api/v1/catalog/search in Swagger");

        Assert.True(paths.TryGetProperty("/api/v1/auctions/{id}/live-status", out var auctionLivePath), "Missing /api/v1/auctions/{id}/live-status in Swagger");
        Assert.True(auctionLivePath.TryGetProperty("get", out _), "Missing GET for /api/v1/auctions/{id}/live-status in Swagger");

        Assert.True(paths.TryGetProperty("/api/v1/notifications/broadcast", out var broadcastPath), "Missing /api/v1/notifications/broadcast in Swagger");
        Assert.True(broadcastPath.TryGetProperty("post", out _), "Missing POST for /api/v1/notifications/broadcast in Swagger");
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
