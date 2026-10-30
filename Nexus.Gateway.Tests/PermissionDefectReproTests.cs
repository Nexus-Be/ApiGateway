namespace Nexus.Gateway.Tests;

using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

public sealed class PermissionDefectReproTests
{
    private const string Secret = "NexusGatewaySuperSecretKeyThatIsAtLeast32Bytes!";

    [Fact]
    public async Task Defect_ApiGateway_Forwards_Spoofed_Permissions_On_Unauthenticated_Requests()
    {
        string? forwardedPermissions = null;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        await using var backend = builder.Build();

        backend.MapGet("/api/v1/categories", (HttpContext ctx) =>
        {
            forwardedPermissions = ctx.Request.Headers.TryGetValue("x-user-permissions", out var p) ? p.ToString() : null;
            return Results.Ok(new
            {
                success = true,
                message = "Categories listed",
                data = new { items = Array.Empty<object>(), totalCount = 0 }
            });
        });

        await backend.StartAsync();
        var address = backend.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();

        using var app = new Factory
        {
            Configure = o =>
            {
                o.Services["catalog"].Mode = "Real";
                o.Services["catalog"].Address = address;
            }
        };

        using var client = app.CreateClient();

        // An UNAUTHENTICATED attacker sends a spoofed x-user-permissions header to an anonymous endpoint
        using var requestSpoof = new HttpRequestMessage(HttpMethod.Get, "/gateway/api/v1/categories");
        requestSpoof.Headers.Add("x-user-permissions", "ADMIN.SUPERUSER,DATABASE.DROP");

        using var responseSpoof = await client.SendAsync(requestSpoof);
        Assert.Equal(HttpStatusCode.OK, responseSpoof.StatusCode);

        // INTENT: Gateway must extract permissions ONLY from verified claims in context.User.
        // Unauthenticated requests must NEVER be allowed to forge downstream x-user-permissions headers.
        // BUG: HttpBackendCaller.ForwardContextHeaders forwards context.Request.Headers["x-user-permissions"] on unauthenticated requests!
        // We assert that the forwardedPermissions is NULL (safe behavior).
        // It FAILS because Gateway blindly forwards "ADMIN.SUPERUSER,DATABASE.DROP"!
        Assert.Null(forwardedPermissions);
    }

    [Fact]
    public async Task Defect_ApiGateway_Forwards_Empty_Header_Value_On_Unauthenticated_Requests()
    {
        string? forwardedPermissions = null;
        bool headerPresent = false;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        await using var backend = builder.Build();

        backend.MapGet("/api/v1/categories", (HttpContext ctx) =>
        {
            headerPresent = ctx.Request.Headers.ContainsKey("x-user-permissions");
            forwardedPermissions = ctx.Request.Headers["x-user-permissions"].ToString();
            return Results.Ok(new
            {
                success = true,
                message = "Categories listed",
                data = new { items = Array.Empty<object>(), totalCount = 0 }
            });
        });

        await backend.StartAsync();
        var address = backend.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();

        using var app = new Factory
        {
            Configure = o =>
            {
                o.Services["catalog"].Mode = "Real";
                o.Services["catalog"].Address = address;
            }
        };

        using var client = app.CreateClient();

        // Unauthenticated request with whitespace x-user-permissions header
        using var requestEmpty = new HttpRequestMessage(HttpMethod.Get, "/gateway/api/v1/categories");
        requestEmpty.Headers.Add("x-user-permissions", "   ");

        using var responseEmpty = await client.SendAsync(requestEmpty);

        // INTENT: Gateway must NOT forward empty or whitespace-only header values to downstream services.
        // BUG: HttpBackendCaller forwards the header even when empty or whitespace because TryGetValue does not validate !string.IsNullOrWhiteSpace.
        Assert.False(headerPresent, $"Gateway forwarded an empty header to downstream with value: '{forwardedPermissions}'");
    }

    [Fact]
    public async Task Defect_ApiGateway_Fails_To_Deduplicate_Permissions_With_Different_Casing_Or_Trim_Whitespace()
    {
        string? forwardedPermissions = null;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        await using var backend = builder.Build();

        backend.MapGet("/api/v1/me", (HttpContext ctx) =>
        {
            forwardedPermissions = ctx.Request.Headers["x-user-permissions"].ToString();
            return Results.Ok(new
            {
                success = true,
                data = new { id = "user-1", email = "test@example.com" }
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

        // Create JWT with mixed-casing and whitespace in permission claims
        var claims = new[]
        {
            new Claim("sub", "user-case-test"),
            new Claim("role", "buyer"),
            new Claim("permission", "ORDER_READ"),
            new Claim("permissions", "order_read"), // Same permission, lowercase
            new Claim("permission", " PRODUCT_VIEW ")  // Has leading/trailing whitespace
        };

        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            "tests",
            "shoppingcart-api",
            claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Factory.Secret)),
                SecurityAlgorithms.HmacSha256)
        ));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.GetAsync("/gateway/api/v1/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // INTENT: Deduplicate permissions (case-insensitively) and trim whitespace before joining into header.
        // BUG: HttpBackendCaller uses .Distinct() (ordinal case-sensitive) and does not .Trim().
        // Result is "ORDER_READ,order_read, PRODUCT_VIEW " containing both case variants and spaces!
        Assert.NotNull(forwardedPermissions);
        var permItems = forwardedPermissions.Split(',');

        // 1. Should deduplicate ORDER_READ and order_read into 1 item
        Assert.DoesNotContain("order_read", permItems);
        // 2. Should trim whitespace
        Assert.DoesNotContain(" PRODUCT_VIEW ", permItems);
    }
}
