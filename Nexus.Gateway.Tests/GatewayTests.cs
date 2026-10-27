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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Gateway.Responses;
using Xunit;

namespace Nexus.Gateway.Tests;

public sealed class GatewayTests
{
    [Fact]
    public async Task Mock_returns_contract_and_trace()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var body = new
        {
            username = "buyer01",
            password = "Password123!"
        };

        var response = await client.PostAsJsonAsync("/gateway/api/v1/auth/login", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Mock", response.Headers.GetValues("X-Gateway-Mode").Single());
        Assert.True(response.Headers.Contains("X-Trace-Id"));

        var jsonString = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(jsonString);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(200, root.GetProperty("statusCode").GetInt32());
        Assert.True(root.GetProperty("data").TryGetProperty("accessToken", out _));

        // Invalid input returns 400 ProblemDetails
        var badResponse = await client.PostAsJsonAsync("/gateway/api/v1/auth/login", new { username = "", password = "" });
        Assert.Equal(HttpStatusCode.BadRequest, badResponse.StatusCode);
        var badJson = await badResponse.Content.ReadAsStringAsync();
        using var badDoc = JsonDocument.Parse(badJson);
        Assert.Equal("VAL_INVALID_INPUT", badDoc.RootElement.GetProperty("errorCode").GetString());

        // Unknown route returns 404
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/gateway/api/v1/unknown")).StatusCode);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("wrong", false)]
    [InlineData("shoppingcart-api", true)]
    public async Task Mock_does_not_bypass_jwt(string? audience, bool expired)
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        if (audience is not null)
        {
            client.DefaultRequestHeaders.Authorization = new("Bearer", Factory.Token(audience, expired));
        }

        var response = await client.GetAsync("/gateway/api/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(401, doc.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("AUTH_UNAUTHORIZED", doc.RootElement.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Valid_jwt_and_profile_retrieval()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Factory.Token());

        // GET /me succeeds
        var profileResponse = await client.GetAsync("/gateway/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);
        var profileJson = await profileResponse.Content.ReadAsStringAsync();
        using var profileDoc = JsonDocument.Parse(profileJson);
        Assert.True(profileDoc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(200, profileDoc.RootElement.GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task Register_and_otp_endpoints_flow()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        // 1. Register
        var registerBody = new
        {
            username = "newuser",
            email = "newuser@example.com",
            password = "Password123!",
            firstName = "New",
            lastName = "User"
        };
        var regResp = await client.PostAsJsonAsync("/gateway/api/v1/auth/register", registerBody);
        Assert.Equal(HttpStatusCode.OK, regResp.StatusCode);
        var regDoc = JsonDocument.Parse(await regResp.Content.ReadAsStringAsync());
        Assert.True(regDoc.RootElement.GetProperty("success").GetBoolean());

        // 2. Resend OTP
        var resendResp = await client.PostAsJsonAsync("/gateway/api/v1/auth/otp/resend", new { email = "newuser@example.com" });
        Assert.Equal(HttpStatusCode.OK, resendResp.StatusCode);
        var resendDoc = JsonDocument.Parse(await resendResp.Content.ReadAsStringAsync());
        Assert.True(resendDoc.RootElement.GetProperty("success").GetBoolean());

        // 3. Verify OTP
        var verifyResp = await client.PostAsJsonAsync("/gateway/api/v1/auth/otp/verify", new { email = "newuser@example.com", otp = "123456" });
        Assert.Equal(HttpStatusCode.OK, verifyResp.StatusCode);
        var verifyDoc = JsonDocument.Parse(await verifyResp.Content.ReadAsStringAsync());
        Assert.True(verifyDoc.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task Real_rest_and_mock_can_coexist()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        await using var backend = builder.Build();
        backend.MapPost("/api/v1/auth/login", () => Results.Ok(new
        {
            success = true,
            message = "Login successful.",
            data = new
            {
                accessToken = "real-jwt-token-from-backend",
                refreshToken = "real-refresh-token",
                tokenType = "Bearer",
                expiresIn = 3600
            }
        }));
        await backend.StartAsync();
        var address = backend.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();

        using var app = new Factory
        {
            Configure = o =>
            {
                o.Services["users"].Mode = "Real";
                o.Services["users"].Address = address;
                o.Services["users"].TimeoutSeconds = 1;
            }
        };
        using var client = app.CreateClient();

        // Call login in Real mode -> handled by downstream REST service
        var real = await client.PostAsJsonAsync("/gateway/api/v1/auth/login", new { username = "realuser", password = "Password123!" });
        Assert.Equal(HttpStatusCode.OK, real.StatusCode);
        Assert.Equal("Real", real.Headers.GetValues("X-Gateway-Mode").Single());

        var json = await real.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        var token = doc.RootElement.GetProperty("data").GetProperty("accessToken").GetString();
        Assert.Equal("real-jwt-token-from-backend", token);

        await backend.StopAsync();
        var unavailable = await client.PostAsJsonAsync("/gateway/api/v1/auth/login", new { username = "realuser", password = "Password123!" });
        Assert.Contains(unavailable.StatusCode, new[] { HttpStatusCode.ServiceUnavailable, HttpStatusCode.GatewayTimeout, HttpStatusCode.BadGateway });
        Assert.Equal("Real", unavailable.Headers.GetValues("X-Gateway-Mode").Single());
    }

    [Fact]
    public void Production_rejects_mock_and_plaintext()
    {
        var options = new GatewayOptions { CorsOrigins = ["https://shop.example/"] };
        foreach (var name in GatewayOptions.CanonicalServices)
        {
            options.Services[name] = new() { Address = "https://service.example" };
        }
        Assert.True(options.IsValid(false));

        options.Services["users"].Mode = "Mock";
        Assert.False(options.IsValid(false));

        options.Services["users"].Mode = "Real";
        options.Services["users"].Address = "http://service.example";
        Assert.False(options.IsValid(false));
    }
}
