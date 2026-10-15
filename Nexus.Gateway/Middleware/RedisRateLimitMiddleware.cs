using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
        if (!context.Request.Path.StartsWithSegments("/api"))
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

        try
        {
            var service = GatewaySecurityHelper.DetectServiceForRequest(context, mockEngine);
            var principal = context.User.FindFirst("sub")?.Value;
            var identity = principal ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
            var now = DateTimeOffset.UtcNow;
            var bucket = now.ToUnixTimeSeconds() / options.WindowSeconds;
            var key = $"{options.RedisKeyPrefix}:rate:{service}:{identity}:{bucket}";
            var count = await rateLimitStore.IncrementAsync(key, TimeSpan.FromSeconds(options.WindowSeconds));

            if (count <= options.PermitLimit)
            {
                await _next(context);
                return;
            }

            var remainingSeconds = options.WindowSeconds - (now.ToUnixTimeSeconds() % options.WindowSeconds);
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
        catch (RedisConnectionException ex) when (environment.IsDevelopment() && options.RedisFailOpenInDevelopment)
        {
            _logger.LogWarning(ex, "Redis is unavailable; development rate limiting is failing open.");
            await _next(context);
        }
        catch (RedisTimeoutException ex) when (environment.IsDevelopment() && options.RedisFailOpenInDevelopment)
        {
            _logger.LogWarning(ex, "Redis timed out; development rate limiting is failing open.");
            await _next(context);
        }
        catch (RedisConnectionException ex)
        {
            await WriteUnavailableAsync(context, responseFactory, ex);
        }
        catch (RedisTimeoutException ex)
        {
            await WriteUnavailableAsync(context, responseFactory, ex);
        }
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
