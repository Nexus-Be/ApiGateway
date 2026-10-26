using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Nexus.Gateway.MockEngine;
using Nexus.Gateway.Responses;
using Nexus.Gateway.Services;

namespace Nexus.Gateway.Security;

public static class SecurityConfiguration
{
    public static IServiceCollection AddGatewaySecurity(this IServiceCollection services, IConfiguration configuration, bool isDevelopment)
    {
        var authority = configuration["Jwt:Authority"] ?? configuration["Jwt:Issuer"] ?? "http://localhost:5001";
        var issuer = configuration["Jwt:Issuer"] ?? configuration["Jwt:Authority"] ?? "http://localhost:5001";
        var audience = configuration["Jwt:Audience"] ?? "shoppingcart-api";
        var requireHttpsMetadata = configuration.GetValue<bool?>("Jwt:RequireHttpsMetadata") ?? !isDevelopment;
        var signingKey = configuration["Jwt:Signature"] ?? configuration["Jwt:SigningKey"] ?? configuration["Jwt:Key"];
        var expirationMinutes = configuration.GetValue<int?>("Jwt:ExpirationInMinutes") ?? configuration.GetValue<int?>("Jwt:Expiration") ?? 60;

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.Authority = authority;
                o.Audience = audience;
                o.RequireHttpsMetadata = requireHttpsMetadata;
                o.MapInboundClaims = false;

                var tvp = new TokenValidationParameters
                {
                    ValidateIssuer = !string.IsNullOrEmpty(authority),
                    ValidIssuer = authority,
                    ValidateAudience = !string.IsNullOrEmpty(audience),
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };

                if (!string.IsNullOrEmpty(signingKey))
                {
                    tvp.ValidateIssuerSigningKey = true;
                    tvp.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
                }

                o.TokenValidationParameters = tvp;

                o.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var env = context.HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
                        var opts = context.HttpContext.RequestServices.GetRequiredService<IOptionsMonitor<GatewayOptions>>().CurrentValue;

                        // Only allow Mock Auth in Development
                        if (!env.IsDevelopment())
                        {
                            return Task.CompletedTask;
                        }

                        // Allow explicit test opt-out via header
                        if ((context.Request.Headers.TryGetValue("X-Require-Real-Auth", out var reqReal) &&
                             string.Equals(reqReal.ToString(), "true", StringComparison.OrdinalIgnoreCase)) ||
                            (context.Request.Headers.TryGetValue("X-Mock-Enforce-Auth", out var enforce) &&
                             string.Equals(enforce.ToString(), "true", StringComparison.OrdinalIgnoreCase)))
                        {
                            return Task.CompletedTask;
                        }

                        var authHeader = context.Request.Headers.Authorization.ToString();
                        var runtimeManager = context.HttpContext.RequestServices.GetRequiredService<IGatewayRuntimeManager>();
                        var mockEngine = context.HttpContext.RequestServices.GetService<IMockEngine>();
                        var isEndpointMock = GatewaySecurityHelper.IsRequestInMockMode(context.HttpContext, opts, runtimeManager, mockEngine);
                        var isAuthBypassed = runtimeManager.IsAuthBypassed(context.HttpContext);

                        // Scenario 1: Token provided
                        if (!string.IsNullOrEmpty(authHeader))
                        {
                            var rawToken = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                                ? authHeader["Bearer ".Length..].Trim()
                                : authHeader.Trim();

                            var hasMockHeader = context.Request.Headers.ContainsKey("X-Mock-Role") ||
                                                context.Request.Headers.ContainsKey("X-Mock-User-Id");

                            // If token is a mock token (e.g. from mock login, or "admin", "seller", "buyer") or mock header provided:
                            // Mock tokens are only accepted when the target service is in Mock mode or AuthMode is Bypass
                            if (opts.AllowMockTokenAuth && (isEndpointMock || isAuthBypassed) && context.HttpContext.AllowsMockAuthorizationBypass() &&
                                (IsMockToken(rawToken) || hasMockHeader))
                            {
                                context.Principal = CreateMockPrincipal(context.HttpContext, rawToken, opts, isAuthBypassed);
                                context.Success();
                                return Task.CompletedTask;
                            }

                            // Otherwise, let standard JwtBearerHandler validate real JWTs
                        }
                        // Scenario 2: No token provided (only bypassed when Gateway is in Bypass/Mock mode)
                        else if (opts.AllowAnonymousMockAuthInDevelopment && isAuthBypassed && isEndpointMock &&
                                 context.HttpContext.AllowsMockAuthorizationBypass())
                        {
                            context.Principal = CreateMockPrincipal(context.HttpContext, null, opts, true);
                            context.Success();
                            return Task.CompletedTask;
                        }

                        return Task.CompletedTask;
                    },

                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";
                        var factory = context.HttpContext.RequestServices.GetRequiredService<IApiResponseFactory>();
                        var error = factory.CreateFailure(
                            title: "Authentication required",
                            status: StatusCodes.Status401Unauthorized,
                            detail: "A valid Bearer token is required to access this resource.",
                            errorCode: "AUTH_UNAUTHORIZED",
                            type: "https://tools.ietf.org/html/rfc9110#section-15.5.2",
                            traceId: ApiResponseFactory.ResolveTraceId(context.HttpContext)
                        );
                        await context.Response.WriteAsJsonAsync(error);
                    },

                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/json";
                        var factory = context.HttpContext.RequestServices.GetRequiredService<IApiResponseFactory>();
                        var error = factory.CreateFailure(
                            title: "Access denied",
                            status: StatusCodes.Status403Forbidden,
                            detail: "You do not have the required permissions or roles to perform this action.",
                            errorCode: "AUTH_FORBIDDEN",
                            type: "https://tools.ietf.org/html/rfc9110#section-15.5.4",
                            traceId: ApiResponseFactory.ResolveTraceId(context.HttpContext)
                        );
                        await context.Response.WriteAsJsonAsync(error);
                    }
                };
            });

        services.AddAuthorization(o =>
        {
            o.AddPolicy(GatewayPolicies.Authenticated, p => p.RequireAuthenticatedUser().RequireClaim("sub"));
            o.AddPolicy(GatewayPolicies.Customer, p => p.RequireAuthenticatedUser().RequireClaim("sub"));
            o.AddPolicy(GatewayPolicies.Seller, p => p.RequireAuthenticatedUser().RequireRole("seller", "admin", "Seller", "Admin"));
            o.AddPolicy(GatewayPolicies.Staff, p => p.RequireAuthenticatedUser().RequireRole("staff", "admin", "Staff", "Admin"));
            o.AddPolicy(GatewayPolicies.Admin, p => p.RequireAuthenticatedUser().RequireRole("admin", "Admin"));
            o.AddPolicy(GatewayPolicies.GatewayAdmin, p => p.RequireAuthenticatedUser().RequireRole("admin", "Admin"));
        });

        return services;
    }

    private static bool IsMockToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        if (token.Contains("mock-token", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(token, "admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "seller", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "staff", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "buyer", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (token.StartsWith("mock-", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static ClaimsPrincipal CreateMockPrincipal(HttpContext context, string? token, GatewayOptions opts, bool isAuthBypassed)
    {
        var request = context.Request;
        string? role = null;
        string? userId = null;

        if (request.Headers.TryGetValue("X-Mock-Role", out var headerRole) && !string.IsNullOrWhiteSpace(headerRole))
        {
            role = headerRole.ToString().ToLowerInvariant();
        }

        if (request.Headers.TryGetValue("X-Mock-User-Id", out var headerUserId) && !string.IsNullOrWhiteSpace(headerUserId))
        {
            userId = headerUserId.ToString();
        }

        if (string.IsNullOrEmpty(role) && !string.IsNullOrEmpty(token))
        {
            var lowerToken = token.ToLowerInvariant();
            if (lowerToken.Contains("admin")) role = "admin";
            else if (lowerToken.Contains("staff")) role = "staff";
            else if (lowerToken.Contains("seller")) role = "seller";
            else if (lowerToken.Contains("buyer") || lowerToken.Contains("development-only")) role = "buyer";
        }

        if (string.IsNullOrEmpty(role))
        {
            if (isAuthBypassed)
            {
                var requiredPolicy = GatewaySecurityHelper.ResolveRequiredPolicy(context);
                if (requiredPolicy is "admin") role = "admin";
                else if (requiredPolicy is "staff") role = "staff";
                else if (requiredPolicy is "seller") role = "seller";
                else role = opts.DefaultMockRole;
            }
            else
            {
                role = opts.DefaultMockRole;
            }
        }

        userId ??= opts.DefaultMockUserId;

        var claims = new List<Claim>
        {
            new("sub", userId),
            new("role", role),
            new(ClaimTypes.Name, $"Mock User ({role})"),
            new(ClaimTypes.Email, $"{role}@mcs.local")
        };

        // Implied roles for admin/staff/seller
        if (role == "admin")
        {
            claims.Add(new Claim("role", "staff"));
            claims.Add(new Claim("role", "seller"));
            claims.Add(new Claim("role", "buyer"));
        }
        else if (role == "staff")
        {
            claims.Add(new Claim("role", "buyer"));
        }
        else if (role == "seller")
        {
            claims.Add(new Claim("role", "buyer"));
        }

        var identity = new ClaimsIdentity(claims, "MockDevScheme", "sub", "role");
        return new ClaimsPrincipal(identity);
    }
}
