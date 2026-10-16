using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexus.Gateway.MockEngine;
using Nexus.Gateway.Services;

namespace Nexus.Gateway.Security;

public sealed class MockSecurityAdapterMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MockSecurityAdapterMiddleware> _logger;

    public MockSecurityAdapterMiddleware(RequestDelegate _next, ILogger<MockSecurityAdapterMiddleware> logger)
    {
        this._next = _next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IWebHostEnvironment env,
        IOptionsMonitor<GatewayOptions> options,
        IGatewayRuntimeManager runtimeManager,
        IMockEngine? mockEngine = null)
    {
        if (!env.IsDevelopment())
        {
            await _next(context);
            return;
        }

        // Check if client explicitly requests strict auth enforcement on mock
        if (context.Request.Headers.TryGetValue("X-Mock-Enforce-Auth", out var enforce) &&
            string.Equals(enforce.ToString(), "true", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (context.Request.Headers.TryGetValue("X-Require-Real-Auth", out var reqReal) &&
            string.Equals(reqReal.ToString(), "true", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var currentOptions = options.CurrentValue;
        var isMock = GatewaySecurityHelper.IsRequestInMockMode(context, currentOptions, runtimeManager, mockEngine);
        var isAuthBypassed = runtimeManager.IsAuthBypassed(context);

        // ONLY bypass auth when Gateway is explicitly in Bypass/Mock mode:
        if (currentOptions.AllowAnonymousMockAuthInDevelopment && isAuthBypassed && isMock &&
            context.AllowsMockAuthorizationBypass())
        {
            var authHeader = context.Request.Headers.Authorization.ToString();
            var hasRealToken = !string.IsNullOrEmpty(authHeader) &&
                               !authHeader.Contains("mock-token", StringComparison.OrdinalIgnoreCase) &&
                               !authHeader.Equals("Bearer seller", StringComparison.OrdinalIgnoreCase) &&
                               !authHeader.Equals("Bearer admin", StringComparison.OrdinalIgnoreCase) &&
                               !authHeader.Equals("Bearer staff", StringComparison.OrdinalIgnoreCase) &&
                               !authHeader.Equals("Bearer buyer", StringComparison.OrdinalIgnoreCase);

            // If no token or client is using mock auth:
            if (string.IsNullOrEmpty(authHeader) || !hasRealToken)
            {
                var requiredPolicy = GatewaySecurityHelper.ResolveRequiredPolicy(context);
                var assignedRole = ResolveRole(context, requiredPolicy, currentOptions);
                var userId = ResolveUserId(context, currentOptions);

                var claims = new List<Claim>
                {
                    new("sub", userId),
                    new("role", assignedRole),
                    new(ClaimTypes.Name, $"Mock User ({assignedRole})"),
                    new(ClaimTypes.Email, $"{assignedRole}@mcs.local")
                };

                // Implied roles for admin/staff/seller
                if (assignedRole == "admin")
                {
                    claims.Add(new Claim("role", "staff"));
                    claims.Add(new Claim("role", "seller"));
                    claims.Add(new Claim("role", "buyer"));
                }
                else if (assignedRole == "staff")
                {
                    claims.Add(new Claim("role", "buyer"));
                }
                else if (assignedRole == "seller")
                {
                    claims.Add(new Claim("role", "buyer"));
                }

                var identity = new ClaimsIdentity(claims, "MockAdaptiveScheme", "sub", "role");
                context.User = new ClaimsPrincipal(identity);
                context.Items["IsMockBypassedAuth"] = true;

                _logger.LogDebug("MockSecurityAdapter: Gateway is Bypass. Granted adaptive role '{Role}' for policy '{Policy}' on {Path}",
                    assignedRole, requiredPolicy, context.Request.Path);
            }
        }

        await _next(context);
    }

    private static string ResolveRole(HttpContext context, string requiredPolicy, GatewayOptions opts)
    {
        // 1. Explicit header override has highest priority
        if (context.Request.Headers.TryGetValue("X-Mock-Role", out var headerRole) && !string.IsNullOrWhiteSpace(headerRole))
        {
            return headerRole.ToString().ToLowerInvariant();
        }

        // 2. Token keyword (e.g. Bearer seller, Bearer admin)
        var authHeader = context.Request.Headers.Authorization.ToString().ToLowerInvariant();
        if (authHeader.Contains("admin")) return "admin";
        if (authHeader.Contains("staff")) return "staff";
        if (authHeader.Contains("seller")) return "seller";
        if (authHeader.Contains("buyer")) return "buyer";

        // 3. Adaptive role matching the required policy
        if (requiredPolicy is "admin") return "admin";
        if (requiredPolicy is "staff") return "staff";
        if (requiredPolicy is "seller") return "seller";

        // 4. Default mock role
        return opts.DefaultMockRole;
    }

    private static string ResolveUserId(HttpContext context, GatewayOptions opts)
    {
        if (context.Request.Headers.TryGetValue("X-Mock-User-Id", out var headerId) && !string.IsNullOrWhiteSpace(headerId))
        {
            return headerId.ToString();
        }
        return opts.DefaultMockUserId;
    }
}

public static class MockSecurityAdapterExtensions
{
    public static IApplicationBuilder UseMockSecurityAdapter(this IApplicationBuilder app)
    {
        return app.UseMiddleware<MockSecurityAdapterMiddleware>();
    }
}
