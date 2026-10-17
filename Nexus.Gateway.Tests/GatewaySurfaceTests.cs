using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using ShoppingCart.Contracts.V1;
using Xunit;

namespace Nexus.Gateway.Tests;

public sealed class GatewaySurfaceTests
{
    private sealed record Endpoint(string Method, string Path, string? Body, string? Role, string Service, string Operation);

    private static readonly Endpoint[] Surface =
    [
        // auth (public)
        new("POST", "/gateway/api/v1/auth/register", """{"username":"newbie","email":"newbie@mcs.local","password":"Passw0rd!"}""", null, "users", "register"),
        new("POST", "/gateway/api/v1/auth/login", """{"username":"buyer01","password":"Passw0rd!"}""", null, "users", "login"),
        // users
        new("GET", "/gateway/api/v1/users/me", null, "buyer", "users", "profile"),
        new("PUT", "/gateway/api/v1/users/me", """{"displayName":"Tên mới"}""", "buyer", "users", "profile-update"),
        new("POST", "/gateway/api/v1/users/me/seller-upgrade", """{"shopName":"Shop A"}""", "buyer", "users", "seller-upgrade"),
        // catalog đọc công khai
        new("GET", "/gateway/api/v1/catalog/categories", null, null, "catalog", "categories"),
        new("GET", "/gateway/api/v1/catalog/products?page=1&pageSize=20", null, null, "catalog", "products"),
        new("GET", "/gateway/api/v1/catalog/products/11111111-1111-1111-1111-111111111111", null, null, "catalog", "product"),
        // catalog quản trị (admin)
        new("POST", "/gateway/api/v1/catalog/categories", """{"name":"Phụ kiện"}""", "admin", "catalog", "category-create"),
        new("PUT", "/gateway/api/v1/catalog/categories/33333333-3333-3333-3333-000000000001", """{"name":"Thiết bị"}""", "admin", "catalog", "category-update"),
        new("DELETE", "/gateway/api/v1/catalog/categories/33333333-3333-3333-3333-000000000001", null, "admin", "catalog", "category-delete"),
        // catalog của seller
        new("GET", "/gateway/api/v1/seller/products", null, "seller", "catalog", "seller-products"),
        new("POST", "/gateway/api/v1/seller/products", """{"name":"Bàn phím","price":"1250000.00","available":10}""", "seller", "catalog", "product-create"),
        new("PUT", "/gateway/api/v1/seller/products/22222222-2222-2222-2222-000000000001", """{"name":"Bàn phím v2","price":"1350000.00"}""", "seller", "catalog", "product-update"),
        new("DELETE", "/gateway/api/v1/seller/products/22222222-2222-2222-2222-000000000001", null, "seller", "catalog", "product-delete"),
        new("GET", "/gateway/api/v1/seller/inventory", null, "seller", "catalog", "inventory"),
        new("PUT", "/gateway/api/v1/seller/inventory/22222222-2222-2222-2222-000000000001", """{"available":100,"reason":"kiểm kê"}""", "seller", "catalog", "inventory-adjust"),
        // commerce
        new("GET", "/gateway/api/v1/commerce/cart", null, "buyer", "commerce", "cart"),
        new("POST", "/gateway/api/v1/commerce/cart/items", """{"productId":"22222222-2222-2222-2222-000000000001","quantity":2}""", "buyer", "commerce", "cart-item-add"),
        new("PATCH", "/gateway/api/v1/commerce/cart/items/22222222-2222-2222-2222-000000000001", """{"quantity":3}""", "buyer", "commerce", "cart-item-update"),
        new("DELETE", "/gateway/api/v1/commerce/cart/items/22222222-2222-2222-2222-000000000001", null, "buyer", "commerce", "cart-item-remove"),
        new("DELETE", "/gateway/api/v1/commerce/cart", null, "buyer", "commerce", "cart-clear"),
        new("GET", "/gateway/api/v1/commerce/orders?page=1&pageSize=20", null, "buyer", "commerce", "orders"),
        new("GET", "/gateway/api/v1/commerce/orders/88888888-8888-8888-8888-000000000001", null, "buyer", "commerce", "order"),
        new("POST", "/gateway/api/v1/commerce/orders", """{"cartId":"77777777-7777-7777-7777-000000000001"}""", "buyer", "commerce", "order-created"),
        new("GET", "/gateway/api/v1/seller/orders", null, "seller", "commerce", "seller-orders"),
        new("GET", "/gateway/api/v1/admin/orders", null, "staff", "commerce", "admin-orders"),
        // admin
        new("GET", "/gateway/api/v1/admin/users", null, "staff", "users", "users"),
        new("POST", "/gateway/api/v1/admin/users", """{"username":"nhanvien1","email":"nhanvien1@mcs.local","password":"Passw0rd!","roles":["staff"]}""", "admin", "users", "user-create"),
        new("POST", "/gateway/api/v1/admin/users/11111111-1111-1111-1111-000000000004/roles", """{"roles":["buyer","seller"]}""", "admin", "users", "user-roles"),
        new("POST", "/gateway/api/v1/admin/sellers/55555555-5555-5555-5555-000000000001/approve", """{"approve":true}""", "staff", "users", "seller-approve"),
        new("GET", "/gateway/api/v1/admin/roles", null, "admin", "users", "roles"),
        new("GET", "/gateway/api/v1/admin/permissions", null, "admin", "users", "permissions"),
        // log (staff đọc; admin bật/tắt từ điển)
        new("GET", "/gateway/api/v1/log/logs?page=1&pageSize=20", null, "staff", "log", "logs"),
        new("GET", "/gateway/api/v1/log/logs/99999999-9999-9999-9999-000000000001", null, "staff", "log", "log-entry"),
        new("GET", "/gateway/api/v1/log/event-types", null, "staff", "log", "log-event-types"),
        new("PUT", "/gateway/api/v1/log/event-types/ProductCreated", """{"enabled":false}""", "admin", "log", "log-event-type-toggle"),
    ];

    private static HttpRequestMessage Request(Endpoint endpoint, string? token)
    {
        var request = new HttpRequestMessage(new HttpMethod(endpoint.Method), endpoint.Path);
        if (endpoint.Body is not null)
            request.Content = new StringContent(endpoint.Body, System.Text.Encoding.UTF8, "application/json");
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (endpoint.Method == "POST" && endpoint.Path.EndsWith("/orders"))
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return request;
    }

    [Fact]
    public async Task Mock_serves_every_operation_in_the_surface()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        var failures = new List<string>();

        foreach (var endpoint in Surface)
        {
            var token = endpoint.Role is null ? null : Factory.TokenFor("real-user", endpoint.Role!);
            using var response = await client.SendAsync(Request(endpoint, token));
            var body = await response.Content.ReadAsStringAsync();

            var expectedStatus = (endpoint.Path == "/gateway/api/v1/commerce/orders" && endpoint.Method == "POST") ||
                                 (endpoint.Path == "/gateway/api/v1/admin/users" && endpoint.Method == "POST")
                                 ? HttpStatusCode.Created
                                 : HttpStatusCode.OK;

            if (response.StatusCode != expectedStatus || body.Length < 3)
            {
                failures.Add($"{endpoint.Method} {endpoint.Path} -> {(int)response.StatusCode}: {body}");
            }
            else if (!response.Headers.TryGetValues("X-Gateway-Mode", out var mode) || mode.Single() != "Mock")
            {
                failures.Add($"{endpoint.Method} {endpoint.Path} -> missing X-Gateway-Mode: Mock");
            }
            else
            {
                using var doc = JsonDocument.Parse(body);
                if (!doc.RootElement.TryGetProperty("success", out var successProp) || !successProp.GetBoolean())
                {
                    failures.Add($"{endpoint.Method} {endpoint.Path} -> missing success: true in response wrapper");
                }
            }
        }

        Assert.Empty(failures);
        Assert.Equal(37, Surface.Length);
    }

    [Fact]
    public async Task Role_matrix_rejects_missing_and_wrong_roles()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        foreach (var endpoint in Surface.Where(x => x.Role is not null))
        {
            using var anonymous = await client.SendAsync(Request(endpoint, null));
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

            var wrongRole = endpoint.Role switch
            {
                "admin" => "seller",
                "staff" => "buyer",
                _ => "buyer"
            };

            if (wrongRole != endpoint.Role)
            {
                using var forbidden = await client.SendAsync(Request(endpoint, Factory.TokenFor("real-user", wrongRole)));
                Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
            }

            using var allowed = await client.SendAsync(Request(endpoint, Factory.TokenFor("real-user", endpoint.Role!)));
            var expectedStatus = (endpoint.Path == "/gateway/api/v1/commerce/orders" && endpoint.Method == "POST") ||
                                 (endpoint.Path == "/gateway/api/v1/admin/users" && endpoint.Method == "POST")
                                 ? HttpStatusCode.Created
                                 : HttpStatusCode.OK;
            Assert.Equal(expectedStatus, allowed.StatusCode);
        }

        // Admin can access seller
        using var adminOnSeller = await client.SendAsync(Request(Surface.Single(x => x.Path == "/gateway/api/v1/seller/inventory"), Factory.TokenFor("root", "admin")));
        Assert.Equal(HttpStatusCode.OK, adminOnSeller.StatusCode);

        // Staff cannot access admin-only
        using var staffOnAdminOnly = await client.SendAsync(Request(Surface.Single(x => x.Path == "/gateway/api/v1/admin/roles"), Factory.TokenFor("ops", "staff")));
        Assert.Equal(HttpStatusCode.Forbidden, staffOnAdminOnly.StatusCode);
    }

    [Fact]
    public async Task Cors_preflight_allows_write_methods()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        foreach (var method in new[] { "PUT", "PATCH", "DELETE" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Options, "/gateway/api/v1/commerce/cart/items/22222222-2222-2222-2222-000000000001");
            request.Headers.Add("Origin", "http://localhost:3000");
            request.Headers.Add("Access-Control-Request-Method", method);
            request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
            using var response = await client.SendAsync(request);

            var allowed = string.Join(",", response.Headers.TryGetValues("Access-Control-Allow-Methods", out var m) ? m : []);
            Assert.Contains(method, allowed);
        }
    }

    [Fact]
    public async Task Validation_rejects_bad_payloads()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Factory.TokenFor("real-user", "seller"));

        // price not number > 0
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/gateway/api/v1/seller/products", new { name = "x", price = "abc", available = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/gateway/api/v1/seller/products", new { name = "x", price = "-5", available = 1 })).StatusCode);
        // quantity < 1
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/gateway/api/v1/commerce/cart/items", new { productId = Guid.NewGuid().ToString(), quantity = 0 })).StatusCode);
        // page < 1
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/gateway/api/v1/seller/products?page=0")).StatusCode);
    }

    [Fact]
    public async Task Real_mode_walks_the_whole_flow_through_grpc()
    {
        var users = await Stub.StartAsync<TestUsers>();
        var catalog = await Stub.StartAsync<TestCatalogStub>();
        var commerce = await Stub.StartAsync<TestCommerce>();

        try
        {
            using var app = new Factory
            {
                Configure = o =>
                {
                    o.Services["users"].Mode = "Real";
                    o.Services["users"].Address = users;
                    o.Services["catalog"].Mode = "Real";
                    o.Services["catalog"].Address = catalog;
                    o.Services["commerce"].Mode = "Real";
                    o.Services["commerce"].Address = commerce;
                }
            };
            using var client = app.CreateClient();

            // Login
            var login = await client.PostAsJsonAsync("/gateway/api/v1/auth/login", new { username = "buyer01", password = "Passw0rd!" });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            Assert.Equal("Real", login.Headers.GetValues("X-Gateway-Mode").Single());

            var loginBody = await login.Content.ReadAsStringAsync();
            using var loginDoc = JsonDocument.Parse(loginBody);
            Assert.True(loginDoc.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal("stub-token", loginDoc.RootElement.GetProperty("data").GetProperty("accessToken").GetString());
            Assert.Equal("buyer-user", loginDoc.RootElement.GetProperty("data").GetProperty("userId").GetString());

            client.DefaultRequestHeaders.Authorization = new("Bearer", Factory.TokenFor("buyer-user", "buyer"));

            // Catalog products
            var products = await client.GetAsync("/gateway/api/v1/catalog/products?q=keyboard");
            Assert.Equal(HttpStatusCode.OK, products.StatusCode);
            Assert.Equal("Real", products.Headers.GetValues("X-Gateway-Mode").Single());
            var productsBody = await products.Content.ReadAsStringAsync();
            using var productsDoc = JsonDocument.Parse(productsBody);
            Assert.True(productsDoc.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal("stub-product", productsDoc.RootElement.GetProperty("data").GetProperty("items")[0].GetProperty("name").GetString());
            Assert.True(products.Headers.Contains("X-Trace-Id"));

            // Add cart item
            var add = await client.PostAsJsonAsync("/gateway/api/v1/commerce/cart/items", new { productId = Guid.NewGuid().ToString(), quantity = 2 });
            Assert.Equal("Real", add.Headers.GetValues("X-Gateway-Mode").Single());
            Assert.Equal(HttpStatusCode.OK, add.StatusCode);

            // Checkout order
            using var checkout = new HttpRequestMessage(HttpMethod.Post, "/gateway/api/v1/commerce/orders")
            {
                Content = JsonContent.Create(new { cartId = Guid.NewGuid().ToString() })
            };
            checkout.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            var order = await client.SendAsync(checkout);
            Assert.Equal(HttpStatusCode.Created, order.StatusCode);

            var orderBody = await order.Content.ReadAsStringAsync();
            using var orderDoc = JsonDocument.Parse(orderBody);
            Assert.True(orderDoc.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal("Completed", orderDoc.RootElement.GetProperty("data").GetProperty("status").GetString());
            Assert.Equal("Paid", orderDoc.RootElement.GetProperty("data").GetProperty("paymentStatus").GetString());

            // Check metadata forwarding to backend
            Assert.StartsWith("Bearer ", Stub.LastAuthorization);
            Assert.True(Stub.LastAuthorization!.Length > "Bearer ".Length);
            Assert.False(string.IsNullOrEmpty(Stub.LastIdempotencyKey));

            // Error mapping from backend
            var missing = await client.GetAsync("/gateway/api/v1/commerce/orders/88888888-8888-8888-8888-000000000009");
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            var missingBody = await missing.Content.ReadAsStringAsync();
            Assert.DoesNotContain("private backend message", missingBody);
            using var missingDoc = JsonDocument.Parse(missingBody);
            Assert.Equal("NOT_FOUND", missingDoc.RootElement.GetProperty("errorCode").GetString());
        }
        finally
        {
            await Task.WhenAll(Stub.StopAsync(users), Stub.StopAsync(catalog), Stub.StopAsync(commerce));
        }
    }
}

internal static class Stub
{
    public static string? LastAuthorization { get; set; }
    public static string? LastIdempotencyKey { get; set; }
    private static readonly Dictionary<string, WebApplication> Addresses = new();

    public static async Task<string> StartAsync<T>() where T : class
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0, listen => listen.Protocols = HttpProtocols.Http2));
        builder.Services.AddGrpc();
        var app = builder.Build();
        app.MapGrpcService<T>();
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        Addresses[address] = app;
        return address;
    }

    public static Task StopAsync(string address) => Addresses[address].StopAsync();
}

public sealed class TestUsers : UsersApi.UsersApiBase
{
    public override Task<LoginResponse> Login(LoginRequest request, ServerCallContext context) =>
        Task.FromResult(new LoginResponse
        {
            AccessToken = "stub-token",
            UserId = "buyer-user",
            ExpiresAt = "2026-10-11T03:00:00Z",
            Roles = { "buyer" }
        });

    public override Task<UserProfile> GetProfile(UserRequest request, ServerCallContext context)
    {
        Stub.LastAuthorization = context.RequestHeaders.GetValue("authorization");
        return Task.FromResult(new UserProfile { Id = request.UserId, DisplayName = "Stub", Roles = { "buyer" } });
    }
}

public sealed class TestCatalogStub : CatalogApi.CatalogApiBase
{
    public override Task<ProductPage> ListProducts(ProductQuery request, ServerCallContext context) => Task.FromResult(
        new ProductPage
        {
            Items = { new Product { Id = Guid.NewGuid().ToString(), Name = "stub-product", Price = "1250000.00", Currency = "VND" } },
            Total = 1
        });
}

public sealed class TestCommerce : CommerceApi.CommerceApiBase
{
    public override Task<Cart> AddCartItem(CartItemRequest request, ServerCallContext context)
    {
        Stub.LastAuthorization = context.RequestHeaders.GetValue("authorization");
        return Task.FromResult(new Cart { Id = Guid.NewGuid().ToString(), UserId = request.UserId });
    }

    public override Task<Order> CreateOrder(CreateOrderRequest request, ServerCallContext context)
    {
        Stub.LastIdempotencyKey = context.RequestHeaders.GetValue("idempotency-key");
        return Task.FromResult(new Order
        {
            Id = Guid.NewGuid().ToString(),
            UserId = request.UserId,
            Status = "Completed",
            PaymentStatus = "Paid",
            Total = "2500000.00",
            Currency = "VND",
            Items = { new OrderItem { ProductId = Guid.NewGuid().ToString(), ProductName = "stub-product", UnitPrice = "1250000.00", Quantity = 2 } }
        });
    }

    public override Task<Order> GetOrder(OrderRequest request, ServerCallContext context) =>
        throw new RpcException(new Status(StatusCode.NotFound, "private backend message"));
}
