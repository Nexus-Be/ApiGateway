using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Gateway.Responses;

namespace Nexus.Gateway.Security;

public sealed record BackendTag(string Name);

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class BackendTagAttribute : Attribute
{
    public string Name { get; }
    public BackendTagAttribute(string name) => Name = name;
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public sealed class GatewayRateLimitAttribute : Attribute
{
    public string PolicyName { get; }
    public GatewayRateLimitAttribute(string policyName) => PolicyName = policyName;
}

public static class EdgeMiddlewareConfiguration
{
    public static IServiceCollection AddGatewayCors(this IServiceCollection services, GatewayOptions gateway)
    {
        services.AddCors(o => o.AddDefaultPolicy(p =>
        {
            if (gateway.CorsOrigins.Length > 0)
            {
                p.WithOrigins(gateway.CorsOrigins);
            }
            else
            {
                p.AllowAnyOrigin();
            }

            if (gateway.CorsMethods.Length > 0)
            {
                p.WithMethods(gateway.CorsMethods);
            }
            else
            {
                p.AllowAnyMethod();
            }

            if (gateway.CorsHeaders.Length > 0 && !gateway.CorsHeaders.Contains("*"))
            {
                p.WithHeaders(gateway.CorsHeaders);
            }
            else
            {
                p.AllowAnyHeader();
            }

            if (gateway.CorsAllowCredentials && gateway.CorsOrigins.Length > 0 && !gateway.CorsOrigins.Contains("*"))
            {
                p.AllowCredentials();
            }

            p.WithExposedHeaders("X-Trace-Id", "X-Gateway-Mode", "Retry-After");
        }));

        return services;
    }

    public static IServiceCollection AddGatewayRateLimiter(this IServiceCollection services, GatewayOptions gateway)
    {
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
                RateLimitPartition.GetConcurrencyLimiter("gateway_concurrency", _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = gateway.ConcurrencyLimit,
                    QueueLimit = 0
                }));

            // RedisRateLimitMiddleware enforces the distributed per-user/IP API
            // limit. Keep these named policies registered with GetNoLimiter so
            // endpoint metadata stays valid while the global concurrency limiter remains active.
            var policyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "api",
                "auth-strict",
                "auth-login",
                "user-frequent",
                "default"
            };

            if (gateway.RateLimits != null)
            {
                foreach (var key in gateway.RateLimits.Keys)
                {
                    policyNames.Add(key);
                }
            }

            foreach (var policy in policyNames)
            {
                o.AddPolicy(policy, _ => RateLimitPartition.GetNoLimiter(policy));
            }

            o.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/json";

                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry)
                    ? Math.Ceiling(retry.TotalSeconds).ToString(CultureInfo.InvariantCulture)
                    : "60";

                context.HttpContext.Response.Headers.RetryAfter = retryAfter;

                var factory = context.HttpContext.RequestServices.GetRequiredService<IApiResponseFactory>();
                var errorResponse = factory.CreateFailure(
                    title: "Rate limit exceeded",
                    status: StatusCodes.Status429TooManyRequests,
                    detail: "Too many requests. Please slow down and try again later.",
                    errorCode: "RATE_LIMIT_EXCEEDED",
                    type: "https://tools.ietf.org/html/rfc6585#section-4",
                    traceId: ApiResponseFactory.ResolveTraceId(context.HttpContext)
                );

                await context.HttpContext.Response.WriteAsJsonAsync(errorResponse, token);
            };
        });

        return services;
    }
}
