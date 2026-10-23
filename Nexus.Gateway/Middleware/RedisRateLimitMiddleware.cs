using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexus.Gateway.MockEngine;
using Nexus.Gateway.Responses;
using Nexus.Gateway.Security;
using StackExchange.Redis;

namespace Nexus.Gateway.Middleware;

public sealed class RedisRateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RedisRateLimitMiddleware> _logger;

    public RedisRateLimitMiddleware(RequestDelegate next, ILogger<RedisRateLimitMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IDistributedRateLimitStore rateLimitStore,
        IOptionsMonitor<GatewayOptions> optionsMonitor,
        IWebHostEnvironment environment,
        IMockEngine mockEngine,
        IApiResponseFactory responseFactory)
    {
        if (!context.Request.Path.StartsWithSegments("/gateway/api") && !context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<DisableRateLimitingAttribute>() != null)
        {
            await _next(context);
            return;
        }

        var options = optionsMonitor.CurrentValue;
        if (!rateLimitStore.IsAvailable)
        {
            if (environment.IsDevelopment() && options.RedisFailOpenInDevelopment)
            {
                _logger.LogWarning("Redis is unavailable; development rate limiting is failing open.");
                await _next(context);
                return;
            }

            await WriteUnavailableAsync(context, responseFactory, new InvalidOperationException("Redis is not connected."));
            return;
        }

        var policyName = endpoint?.Metadata.GetMetadata<GatewayRateLimitAttribute>()?.PolicyName
            ?? endpoint?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;

        var policy = options.GetRateLimitPolicy(policyName);
        var effectivePolicyName = !string.IsNullOrWhiteSpace(policyName) ? policyName.Trim().ToLowerInvariant() : "default";

        var service = GatewaySecurityHelper.DetectServiceForRequest(context, mockEngine);
        var principal = context.User.FindFirst("sub")?.Value;
        var identity = principal ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        var now = DateTimeOffset.UtcNow;
        var windowSeconds = policy.WindowSeconds;
        var permitLimit = policy.PermitLimit;

        var bucket = now.ToUnixTimeSeconds() / windowSeconds;
        var key = $"{options.RedisKeyPrefix}:rate:{effectivePolicyName}:{service}:{identity}:{bucket}";
        long count;
        try
        {
            count = await rateLimitStore.IncrementAsync(key, TimeSpan.FromSeconds(windowSeconds));
        }
        catch (RedisConnectionException ex) when (environment.IsDevelopment() && options.RedisFailOpenInDevelopment)
        {
            _logger.LogWarning(ex, "Redis is unavailable; development rate limiting is failing open.");
            await _next(context);
            return;
        }
        catch (RedisTimeoutException ex) when (environment.IsDevelopment() && options.RedisFailOpenInDevelopment)
        {
            _logger.LogWarning(ex, "Redis timed out; development rate limiting is failing open.");
            await _next(context);
            return;
        }
        catch (RedisConnectionException ex)
        {
            await WriteUnavailableAsync(context, responseFactory, ex);
            return;
        }
        catch (RedisTimeoutException ex)
        {
            await WriteUnavailableAsync(context, responseFactory, ex);
            return;
        }

        if (count <= permitLimit)
        {
            await _next(context);
            return;
        }

        var remainingSeconds = windowSeconds - (now.ToUnixTimeSeconds() % windowSeconds);
        context.Response.Headers.RetryAfter = Math.Max(1, remainingSeconds).ToString();
        var result = responseFactory.FailureResult(
            context,
            title: "Rate limit exceeded",
            status: StatusCodes.Status429TooManyRequests,
            detail: "Too many requests. Please slow down and try again later.",
            errorCode: "RATE_LIMIT_EXCEEDED",
            type: "https://tools.ietf.org/html/rfc6585#section-4");
        await result.ExecuteAsync(context);
    }

    private async Task WriteUnavailableAsync(HttpContext context, IApiResponseFactory responseFactory, Exception exception)
    {
        _logger.LogError(exception, "Redis is unavailable; rejecting request because distributed rate limiting cannot run.");
        var result = responseFactory.FailureResult(
            context,
            title: "Rate limit service unavailable",
            status: StatusCodes.Status503ServiceUnavailable,
            detail: "The gateway cannot verify the current request limit.",
            errorCode: "RATE_LIMIT_SERVICE_UNAVAILABLE",
            type: "https://tools.ietf.org/html/rfc9110#section-15.6.4");
        await result.ExecuteAsync(context);
    }
}

public interface IDistributedRateLimitStore
{
    bool IsAvailable { get; }

    Task<long> IncrementAsync(string key, TimeSpan expiry);
}

public sealed class RedisRateLimitStore : IDistributedRateLimitStore
{
    private const string IncrementWithExpiry = """
        local current = redis.call('INCR', KEYS[1])
        if current == 1 then
            redis.call('PEXPIRE', KEYS[1], ARGV[1])
        end
        return current
        """;

    private readonly IConnectionMultiplexer _redis;

    public RedisRateLimitStore(IConnectionMultiplexer redis) => _redis = redis;

    public bool IsAvailable => _redis.IsConnected;

    public async Task<long> IncrementAsync(string key, TimeSpan expiry) =>
        (long)await _redis.GetDatabase().ScriptEvaluateAsync(
            IncrementWithExpiry,
            [key],
            [(long)Math.Ceiling(expiry.TotalMilliseconds)]);
}

public static class RedisRateLimitMiddlewareExtensions
{
    public static IApplicationBuilder UseRedisRateLimit(this IApplicationBuilder app) =>
        app.UseMiddleware<RedisRateLimitMiddleware>();
}
