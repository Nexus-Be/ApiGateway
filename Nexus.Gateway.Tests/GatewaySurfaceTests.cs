using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Nexus.Gateway.Tests;

public sealed class GatewaySurfaceTests
{
    private sealed record Endpoint(string Method, string Path, string? Body, string? Role, string Service, string Operation);

    private static readonly Endpoint[] Surface =
    [
        // Auth public endpoints
        new("POST", "/gateway/api/v1/auth/register", """{"username":"newbie","email":"newbie@mcs.local","password":"Password123!","firstName":"New","lastName":"User"}""", null, "users", "register"),
        new("POST", "/gateway/api/v1/auth/otp/verify", """{"email":"verified@mcs.local","otp":"123456"}""", null, "users", "verify-otp"),
        new("POST", "/gateway/api/v1/auth/otp/resend", """{"email":"user@mcs.local"}""", null, "users", "resend-otp"),
        new("POST", "/gateway/api/v1/auth/login", """{"username":"buyer01","password":"Password123!"}""", null, "users", "login"),
        // Users authenticated endpoint
        new("GET", "/gateway/api/v1/me", null, "buyer", "users", "profile"),
        // Gateway control plane
        new("GET", "/gateway/api/v1/gateway/modes", null, "admin", "gateway", "modes")
    ];

    private static HttpRequestMessage Request(Endpoint endpoint, string? token)
    {
        var request = new HttpRequestMessage(new HttpMethod(endpoint.Method), endpoint.Path);
        if (endpoint.Body is not null)
            request.Content = new StringContent(endpoint.Body, System.Text.Encoding.UTF8, "application/json");
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    [Fact]
    public async Task Mock_serves_every_operation_in_the_surface()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        var failures = new List<string>();

        foreach (var endpoint in Surface)
        {
            var token = endpoint.Role is null ? null : Factory.TokenFor("real-user", endpoint.Role!);
            using var response = await client.SendAsync(Request(endpoint, token));
            var body = await response.Content.ReadAsStringAsync();

            var expectedStatus = HttpStatusCode.OK;
            if (response.StatusCode != expectedStatus)
            {
                failures.Add($"[{endpoint.Method}] {endpoint.Path} => status {(int)response.StatusCode} != {(int)expectedStatus}. Body: {body}");
                continue;
            }

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("success", out var s) || !s.GetBoolean())
            {
                failures.Add($"[{endpoint.Method}] {endpoint.Path} => success != true. Body: {body}");
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public async Task Rbac_enforces_required_role_on_authenticated_endpoints()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        // 1. Calling GET /me without token returns 401
        using var unauth = await client.GetAsync("/gateway/api/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, unauth.StatusCode);

        // 2. Calling GET /me with buyer token returns 200
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Factory.TokenFor("user-1", "buyer"));
        using var authResp = await client.GetAsync("/gateway/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, authResp.StatusCode);

        // 3. Calling gateway modes without admin token returns 401/403
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Factory.TokenFor("user-1", "buyer"));
        using var forbidden = await client.GetAsync("/gateway/api/v1/gateway/modes");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        // 4. Calling gateway modes with admin token returns 200
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Factory.TokenFor("admin-1", "admin"));
        using var allowed = await client.GetAsync("/gateway/api/v1/gateway/modes");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task Real_mode_forwards_user_headers_to_downstream_rest()
    {
        string? lastAuthorization = null;
        string? lastPermissions = null;
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        await using var backend = builder.Build();
        backend.MapGet("/api/v1/me", (HttpContext ctx) =>
        {
            lastAuthorization = ctx.Request.Headers["authorization"].ToString();
            lastPermissions = ctx.Request.Headers["x-user-permissions"].ToString();
            return Results.Ok(new
            {
                success = true,
                message = "User profile retrieved successfully.",
                data = new
                {
                    id = "seller-user",
                    displayName = "Stub User",
                    email = "stub@example.com",
                    roles = new[] { "seller" },
                    status = "Active"
                }
            });
        });
        await backend.StartAsync();
        var address = backend.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();

        using var app = new Factory
        {
            Configure = o =>
            {
                o.Services["users"].Mode = "Real";
                o.Services["users"].Address = address;
            }
        };
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Factory.TokenWithPermissions("seller-user", ["seller"], "PRODUCT_CREATE", "PRODUCT_UPDATE"));

        using var response = await client.GetAsync("/gateway/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Real", response.Headers.GetValues("X-Gateway-Mode").Single());
        Assert.NotNull(lastAuthorization);
        Assert.NotNull(lastPermissions);
        Assert.Contains("PRODUCT_CREATE", lastPermissions);
        Assert.Contains("PRODUCT_UPDATE", lastPermissions);
    }
}
