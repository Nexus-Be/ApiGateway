using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexus.Gateway.Middleware;
using Nexus.Gateway.MockEngine;
using Nexus.Gateway.Responses;
using Xunit;

namespace Nexus.Gateway.Tests;

public sealed class EndpointRateLimitingTests
{
    [Fact]
    public async Task Register_Enforces_AuthStrict_Limit_And_Returns_429()
    {
        using var app = new Factory
        {
            Configure = o =>
            {
                o.RateLimits["auth-strict"] = new() { PermitLimit = 5, WindowSeconds = 60 };
                o.RedisFailOpenInDevelopment = false;
            }
        };
        using var client = app.CreateClient();

        var body = new
        {
            username = "testlimituser",
            email = "limituser@example.com",
            password = "Password123!"
        };

        // 5 requests within limit succeed
        for (var i = 0; i < 5; i++)
        {
            var okResp = await client.PostAsJsonAsync("/api/v1/auth/register", body);
            Assert.Equal(HttpStatusCode.OK, okResp.StatusCode);
        }

        // 6th request triggers rate limit -> 429 Too Many Requests
        var limitedResp = await client.PostAsJsonAsync("/api/v1/auth/register", body);
        Assert.Equal(HttpStatusCode.TooManyRequests, limitedResp.StatusCode);
        Assert.True(limitedResp.Headers.Contains("Retry-After"));

        var content = await limitedResp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        Assert.Equal("RATE_LIMIT_EXCEEDED", root.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Different_Policies_Are_Independent_And_Do_Not_Cross_Block()
    {
        using var app = new Factory
        {
            Configure = o =>
            {
                o.RateLimits["auth-strict"] = new() { PermitLimit = 2, WindowSeconds = 60 };
                o.RateLimits["user-frequent"] = new() { PermitLimit = 50, WindowSeconds = 60 };
                o.RedisFailOpenInDevelopment = false;
            }
        };
        using var client = app.CreateClient();

        var regBody = new
        {
            username = "isolateduser",
            email = "isolated@example.com",
            password = "Password123!"
        };

        // Exhaust auth-strict limit
        for (var i = 0; i < 2; i++)
        {
            var r = await client.PostAsJsonAsync("/api/v1/auth/register", regBody);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }

        // 3rd call to register is blocked
        var blockedRegister = await client.PostAsJsonAsync("/api/v1/auth/register", regBody);
        Assert.Equal(HttpStatusCode.TooManyRequests, blockedRegister.StatusCode);

        // But calling GET /api/v1/users/me (user-frequent policy) is NOT blocked
        var token = Factory.TokenFor("isolated-user-id", "buyer");
        var userRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/me");
        userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var userResp = await client.SendAsync(userRequest);
        Assert.Equal(HttpStatusCode.OK, userResp.StatusCode);
    }

    [Fact]
    public async Task VerifyOtp_And_ResendOtp_Enforce_AuthStrict_Policy()
    {
        using var app = new Factory
        {
            Configure = o =>
            {
                o.RateLimits["auth-strict"] = new() { PermitLimit = 3, WindowSeconds = 60 };
                o.RedisFailOpenInDevelopment = false;
            }
        };
        using var client = app.CreateClient();

        var resendBody = new { email = "otpuser@example.com" };

        for (var i = 0; i < 3; i++)
        {
            var okResp = await client.PostAsJsonAsync("/api/v1/auth/resend-otp", resendBody);
            Assert.Equal(HttpStatusCode.OK, okResp.StatusCode);
        }

        var limited = await client.PostAsJsonAsync("/api/v1/auth/resend-otp", resendBody);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [Fact]
    public void GatewayOptions_GetRateLimitPolicy_Resolves_Expected_Fallbacks()
    {
        var options = new GatewayOptions
        {
            PermitLimit = 80,
            WindowSeconds = 45,
            RateLimits =
            {
                ["auth-strict"] = new() { PermitLimit = 5, WindowSeconds = 60 },
                ["default"] = new() { PermitLimit = 100, WindowSeconds = 60 }
            }
        };

        // Specific configured policy
        var strictPolicy = options.GetRateLimitPolicy("auth-strict");
        Assert.Equal(5, strictPolicy.PermitLimit);
        Assert.Equal(60, strictPolicy.WindowSeconds);

        // Unconfigured policy falls back to "default" in RateLimits
        var unknownPolicy = options.GetRateLimitPolicy("unknown-policy");
        Assert.Equal(100, unknownPolicy.PermitLimit);
        Assert.Equal(60, unknownPolicy.WindowSeconds);

        // When "default" does not exist in RateLimits, falls back to GatewayOptions PermitLimit/WindowSeconds
        options.RateLimits.Remove("default");
        var fallbackPolicy = options.GetRateLimitPolicy("unregistered");
        Assert.Equal(80, fallbackPolicy.PermitLimit);
        Assert.Equal(45, fallbackPolicy.WindowSeconds);

        var nullPolicy = options.GetRateLimitPolicy(null);
        Assert.Equal(80, nullPolicy.PermitLimit);
        Assert.Equal(45, nullPolicy.WindowSeconds);
    }

    [Fact]
    public async Task RedisRateLimitMiddleware_Skips_WhenEndpointHasDisableRateLimiting()
    {
        var middleware = new RedisRateLimitMiddleware(
            _ => Task.CompletedTask,
            NullLogger<RedisRateLimitMiddleware>.Instance);

        var store = new CountingRateLimitStore();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/test";
        context.RequestServices = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();

        // Create an endpoint with DisableRateLimitingAttribute metadata
        var endpoint = new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new DisableRateLimitingAttribute()),
            "DisabledRateLimitEndpoint");
        context.SetEndpoint(endpoint);

        var options = new GatewayOptions { RedisFailOpenInDevelopment = false };
        await middleware.InvokeAsync(
            context,
            store,
            new StaticOptionsMonitor(options),
            new StaticEnvironment("Production"),
            new DummyMockEngine(),
            new ApiResponseFactory());

        // Since rate limiting is disabled, store should not have been called
        Assert.Equal(0, store.CallCount);
    }

    private sealed class CountingRateLimitStore : IDistributedRateLimitStore
    {
        public bool IsAvailable => true;
        public int CallCount { get; private set; }

        public Task<long> IncrementAsync(string key, TimeSpan expiry)
        {
            CallCount++;
            return Task.FromResult(1L);
        }
    }

    private sealed class StaticOptionsMonitor(GatewayOptions options) : IOptionsMonitor<GatewayOptions>
    {
        public GatewayOptions CurrentValue => options;
        public GatewayOptions Get(string? name) => options;
        public IDisposable? OnChange(Action<GatewayOptions, string?> listener) => null;
    }

    private sealed class StaticEnvironment(string env) : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = env;
        public string ApplicationName { get; set; } = "Nexus.Gateway.Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class DummyMockEngine : IMockEngine
    {
        public IReadOnlyList<MockEndpointDefinition> GetDefinitions() => [];
        public MockRouteMatch Match(string httpMethod, string requestPath) => MockRouteMatch.Failed;
        public void Reload() { }
    }
}
