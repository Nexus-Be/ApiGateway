using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Nexus.Gateway.Middleware;

namespace Nexus.Gateway.Tests;

public sealed class Factory : WebApplicationFactory<Program>
{
    public const string Secret = "test-only-signing-key-not-used-by-production-123456";
    public Action<GatewayOptions>? Configure { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDistributedRateLimitStore>();
            services.AddSingleton<IDistributedRateLimitStore, TestRateLimitStore>();
            services.PostConfigure<GatewayOptions>(o =>
            {
                foreach (var service in o.Services.Values)
                {
                    service.Mode = "Mock";
                }
                o.AllowAnonymousMockAuthInDevelopment = true;
                o.RedisKeyPrefix = $"nexus:gateway:tests:{Guid.NewGuid():N}";
                Configure?.Invoke(o);
            });

            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o =>
            {
                o.Authority = null;
                o.ConfigurationManager = null;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = "tests",
                    ValidateAudience = true,
                    ValidAudience = "shoppingcart-api",
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = "sub",
                    RoleClaimType = "role",
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret))
                };
            });
        });
    }

    public static string TokenFor(string subject, params string[] roles)
    {
        var claims = new List<Claim> { new("sub", subject) };
        claims.AddRange(roles.Select(role => new Claim("role", role)));

        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            "tests",
            "shoppingcart-api",
            claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
                SecurityAlgorithms.HmacSha256)
        ));
    }

    public static string Token(string audience = "shoppingcart-api", bool expired = false)
    {
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            "tests",
            audience,
            [new Claim("sub", "real-user")],
            expires: DateTime.UtcNow.AddMinutes(expired ? -5 : 5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
                SecurityAlgorithms.HmacSha256)
        ));
    }

    private sealed class TestRateLimitStore : IDistributedRateLimitStore
    {
        private readonly ConcurrentDictionary<string, (long Count, DateTimeOffset ExpiresAt)> _counters = new();

        public bool IsAvailable => true;

        public Task<long> IncrementAsync(string key, TimeSpan expiry)
        {
            var now = DateTimeOffset.UtcNow;
            var result = _counters.AddOrUpdate(
                key,
                _ => (1, now.Add(expiry)),
                (_, current) => current.ExpiresAt <= now ? (1, now.Add(expiry)) : (current.Count + 1, current.ExpiresAt));
            return Task.FromResult(result.Count);
        }
    }
}
