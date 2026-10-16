using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Gateway.Middleware;
using Nexus.Gateway.MockEngine;
using Nexus.Gateway.Responses;
using StackExchange.Redis;

namespace Nexus.Gateway.Tests;

public sealed class RedisRateLimitMiddlewareRegressionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task DownstreamRedisConnectionException_IsPropagatedWithoutRetry(bool development, bool failOpen)
    {
        var downstreamCalls = 0;
        var failure = ConnectionFailure();
        var middleware = CreateMiddleware(_ =>
        {
            downstreamCalls++;
            throw failure;
        });

        var caught = await Assert.ThrowsAsync<RedisConnectionException>(() => InvokeAsync(
            middleware, new TestStore(), development, failOpen));

        Assert.Same(failure, caught);
        Assert.Equal(1, downstreamCalls);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task DownstreamRedisTimeoutException_IsPropagatedWithoutRetry(bool development, bool failOpen)
    {
        var downstreamCalls = 0;
        var failure = TimeoutFailure();
        var middleware = CreateMiddleware(_ =>
        {
            downstreamCalls++;
            throw failure;
        });

        var caught = await Assert.ThrowsAsync<RedisTimeoutException>(() => InvokeAsync(
            middleware, new TestStore(), development, failOpen));

        Assert.Same(failure, caught);
        Assert.Equal(1, downstreamCalls);
    }

    [Theory]
    [InlineData(false, false, false, 503)]
    [InlineData(false, true, false, 503)]
    [InlineData(true, false, false, 503)]
    [InlineData(true, true, false, 200)]
    [InlineData(false, false, true, 503)]
    [InlineData(false, true, true, 503)]
    [InlineData(true, false, true, 503)]
    [InlineData(true, true, true, 200)]
    public async Task StoreRedisFailure_PreservesFailOpenAndFailClosed(
        bool development, bool failOpen, bool timeout, int expectedStatus)
    {
        var downstreamCalls = 0;
        var middleware = CreateMiddleware(_ =>
        {
            downstreamCalls++;
            return Task.CompletedTask;
        });
        var store = new TestStore { Failure = timeout ? TimeoutFailure() : ConnectionFailure() };
        var context = await InvokeAsync(middleware, store, development, failOpen);

        Assert.Equal(1, store.IncrementCalls);
        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.Equal(expectedStatus == 200 ? 1 : 0, downstreamCalls);
    }

    private static RedisRateLimitMiddleware CreateMiddleware(RequestDelegate next) =>
        new(next, NullLogger<RedisRateLimitMiddleware>.Instance);

    private static async Task<DefaultHttpContext> InvokeAsync(
        RedisRateLimitMiddleware middleware, TestStore store, bool development, bool failOpen)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/catalog/items";
        context.RequestServices = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        context.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(
            context,
            store,
            new TestOptionsMonitor(new GatewayOptions { RedisFailOpenInDevelopment = failOpen }),
            new TestEnvironment { EnvironmentName = development ? "Development" : "Production" },
            new TestMockEngine(),
            new ApiResponseFactory());
        return context;
    }

    private static RedisConnectionException ConnectionFailure() =>
        new(ConnectionFailureType.UnableToConnect, "Redis connection failed");

    private static RedisTimeoutException TimeoutFailure() => new("Redis timed out", CommandStatus.Unknown);

    private sealed class TestStore : IDistributedRateLimitStore
    {
        public bool IsAvailable => true;
        public Exception? Failure { get; init; }
        public int IncrementCalls { get; private set; }

        public Task<long> IncrementAsync(string key, TimeSpan expiry)
        {
            IncrementCalls++;
            return Failure is null ? Task.FromResult(1L) : Task.FromException<long>(Failure);
        }
    }

    private sealed class TestOptionsMonitor(GatewayOptions options) : IOptionsMonitor<GatewayOptions>
    {
        public GatewayOptions CurrentValue => options;
        public GatewayOptions Get(string? name) => options;
        public IDisposable? OnChange(Action<GatewayOptions, string?> listener) => null;
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Nexus.Gateway.Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TestMockEngine : IMockEngine
    {
        public IReadOnlyList<MockEndpointDefinition> GetDefinitions() => [];
        public MockRouteMatch Match(string httpMethod, string requestPath) => MockRouteMatch.Failed;
        public void Reload() { }
    }
}
