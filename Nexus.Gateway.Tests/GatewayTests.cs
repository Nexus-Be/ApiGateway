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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Gateway.Responses;
using ShoppingCart.Contracts.V1;
using Xunit;

namespace Nexus.Gateway.Tests;

public sealed class GatewayTests
{
    [Fact]
    public async Task Mock_returns_contract_and_trace()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var response = await client.GetAsync("/gateway/api/v1/catalog/products");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Mock", response.Headers.GetValues("X-Gateway-Mode").Single());
        Assert.True(response.Headers.Contains("X-Trace-Id"));

        var jsonString = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(jsonString);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(200, root.GetProperty("statusCode").GetInt32());
        Assert.True(root.GetProperty("data").GetProperty("items").GetArrayLength() > 0);
        Assert.Equal(1, root.GetProperty("data").GetProperty("pagination").GetProperty("pageIndex").GetInt32());

        // Invalid pageSize returns 400
        var badResponse = await client.GetAsync("/gateway/api/v1/catalog/products?pageSize=101");
        Assert.Equal(HttpStatusCode.BadRequest, badResponse.StatusCode);
        var badJson = await badResponse.Content.ReadAsStringAsync();
        using var badDoc = JsonDocument.Parse(badJson);
        Assert.Equal("VAL_INVALID_INPUT", badDoc.RootElement.GetProperty("errorCode").GetString());

        // Unknown route returns 404
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/gateway/api/v1/unknown")).StatusCode);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("wrong", false)]
    [InlineData("shoppingcart-api", true)]
    public async Task Mock_does_not_bypass_jwt(string? audience, bool expired)
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        if (audience is not null)
        {
            client.DefaultRequestHeaders.Authorization = new("Bearer", Factory.Token(audience, expired));
        }

        var response = await client.GetAsync("/gateway/api/v1/commerce/cart");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(401, doc.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("AUTH_UNAUTHORIZED", doc.RootElement.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Valid_jwt_and_checkout_validation()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Factory.Token());

        // GET /users/me succeeds
        var profileResponse = await client.GetAsync("/gateway/api/v1/users/me");
        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);
        var profileJson = await profileResponse.Content.ReadAsStringAsync();
        using var profileDoc = JsonDocument.Parse(profileJson);
        Assert.True(profileDoc.RootElement.GetProperty("success").GetBoolean());

        // POST /orders without Idempotency-Key fails with 400
        var body = new { cartId = Guid.NewGuid().ToString() };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/gateway/api/v1/commerce/orders", body)).StatusCode);

        // POST /orders with Idempotency-Key succeeds
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var orderResponse = await client.PostAsJsonAsync("/gateway/api/v1/commerce/orders", body);
        Assert.Equal(HttpStatusCode.Created, orderResponse.StatusCode);
        var orderJson = await orderResponse.Content.ReadAsStringAsync();
        using var orderDoc = JsonDocument.Parse(orderJson);
        Assert.True(orderDoc.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task Checkout_accepts_product_list_and_rejects_bad_lines()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Factory.Token());
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());

        // Mua ngay với danh sách sản phẩm
        var items = new { items = new[] { new { productId = Guid.NewGuid().ToString(), quantity = 2 } } };
        var successResponse = await client.PostAsJsonAsync("/gateway/api/v1/commerce/orders", items);
        Assert.Equal(HttpStatusCode.Created, successResponse.StatusCode);

        // Thiếu cả items lẫn cartId => 400
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/gateway/api/v1/commerce/orders", new { })).StatusCode);

        // quantity < 1 => 400
        var badQuantity = new { items = new[] { new { productId = Guid.NewGuid().ToString(), quantity = 0 } } };
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/gateway/api/v1/commerce/orders", badQuantity)).StatusCode);

        // productId không phải UUID => 400
        var badProduct = new { items = new[] { new { productId = "not-a-uuid", quantity = 1 } } };
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/gateway/api/v1/commerce/orders", badProduct)).StatusCode);
    }

    [Fact]
    public async Task Admin_creates_user_with_roles_and_edge_validations()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Factory.TokenFor("root", "admin"));

        // Admin tạo user thành công
        var body = new { username = "nhanvien1", email = "nhanvien1@mcs.local", password = "Passw0rd!", roles = new[] { "staff" } };
        var created = await client.PostAsJsonAsync("/gateway/api/v1/admin/users", body);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("Mock", created.Headers.GetValues("X-Gateway-Mode").Single());

        // Staff gọi endpoint này bị 403 Forbidden
        client.DefaultRequestHeaders.Authorization = new("Bearer", Factory.TokenFor("ops", "staff"));
        var forbiddenResponse = await client.PostAsJsonAsync("/gateway/api/v1/admin/users", body);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
        var forbiddenJson = await forbiddenResponse.Content.ReadAsStringAsync();
        using var forbiddenDoc = JsonDocument.Parse(forbiddenJson);
        Assert.Equal("AUTH_FORBIDDEN", forbiddenDoc.RootElement.GetProperty("errorCode").GetString());

        client.DefaultRequestHeaders.Authorization = new("Bearer", Factory.TokenFor("root", "admin"));

        // Validate password ngắn, roles lạ, username rỗng => 400
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/gateway/api/v1/admin/users",
            new { username = "x", email = "x@mcs.local", password = "ngan" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/gateway/api/v1/admin/users",
            new { username = "x", email = "x@mcs.local", password = "Passw0rd!", roles = new[] { "superuser" } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/gateway/api/v1/admin/users",
            new { username = "", email = "x@mcs.local", password = "Passw0rd!" })).StatusCode);

        // available != 0 khi PUT seller products => 400
        client.DefaultRequestHeaders.Authorization = new("Bearer", Factory.TokenFor("shop", "seller"));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(
            "/gateway/api/v1/seller/products/22222222-2222-2222-2222-000000000001",
            new { name = "Bàn phím v2", price = "1350000.00", available = 10 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(
            "/gateway/api/v1/seller/products/22222222-2222-2222-2222-000000000001",
            new { name = "Bàn phím v2", price = "1350000.00" })).StatusCode);

        // Từ chối seller không có reason => 400
        client.DefaultRequestHeaders.Authorization = new("Bearer", Factory.TokenFor("ops", "staff"));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(
            "/gateway/api/v1/admin/sellers/55555555-5555-5555-5555-000000000001/approve", new { approve = false })).StatusCode);

        // Phân trang orders của buyer: pageSize > 100 => 400
        client.DefaultRequestHeaders.Authorization = new("Bearer", Factory.TokenFor("buyer-1", "buyer"));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/gateway/api/v1/commerce/orders?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/gateway/api/v1/commerce/orders?page=1&pageSize=5")).StatusCode);
    }

    [Fact]
    public async Task Cors_allows_only_configured_origins()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        foreach (var origin in new[] { "http://localhost:3000", "https://evil.example" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Options, "/gateway/api/v1/commerce/orders");
            request.Headers.Add("Origin", origin);
            request.Headers.Add("Access-Control-Request-Method", "POST");
            request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type,idempotency-key");
            var response = await client.SendAsync(request);
            Assert.Equal(origin == "http://localhost:3000", response.Headers.Contains("Access-Control-Allow-Origin"));
        }
    }

    [Fact]
    public async Task Rate_limit_is_per_service_and_returns_retry_after()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/gateway/api/v1/catalog/products")).StatusCode);
        }

        var response = await client.GetAsync("/gateway/api/v1/catalog/products");
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.True(response.Headers.Contains("Retry-After"));

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(429, doc.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("RATE_LIMIT_EXCEEDED", doc.RootElement.GetProperty("errorCode").GetString());

        // Other service partition is isolated and works
        client.DefaultRequestHeaders.Authorization = new("Bearer", Factory.Token());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/gateway/api/v1/commerce/cart")).StatusCode);
    }

    [Fact]
    public async Task Mock_delay_respects_timeout()
    {
        using var app = new Factory
        {
            Configure = o =>
            {
                o.Services["catalog"].TimeoutSeconds = 1;
                o.Services["catalog"].MockDelayMs = 2000;
            }
        };
        using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.GatewayTimeout, (await client.GetAsync("/gateway/api/v1/catalog/products")).StatusCode);
    }

    [Fact]
    public async Task Reload_changes_one_service_without_restart()
    {
        var mode = "Mock";
        using var app = new Factory
        {
            Configure = o =>
            {
                o.Services["catalog"].Mode = mode;
                o.Services["catalog"].Address = "http://127.0.0.1:1";
            }
        };
        using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/gateway/api/v1/catalog/products")).StatusCode);

        mode = "Real";
        ((IConfigurationRoot)app.Services.GetRequiredService<IConfiguration>()).Reload();
        var response = await client.GetAsync("/gateway/api/v1/catalog/products");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Real", response.Headers.GetValues("X-Gateway-Mode").Single());

        client.DefaultRequestHeaders.Authorization = new("Bearer", Factory.Token());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/gateway/api/v1/commerce/cart")).StatusCode);

        mode = "Mock";
        ((IConfigurationRoot)app.Services.GetRequiredService<IConfiguration>()).Reload();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/gateway/api/v1/catalog/products")).StatusCode);
    }

    [Fact]
    public async Task Configured_mock_error_has_problem_details()
    {
        using var app = new Factory { Configure = o => o.Services["catalog"].MockStatus = 503 };
        using var client = app.CreateClient();

        var response = await client.GetAsync("/gateway/api/v1/catalog/products");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(503, doc.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("MOCK_FAILURE", doc.RootElement.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Real_grpc_and_mock_can_coexist()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0, listen => listen.Protocols = HttpProtocols.Http2));
        builder.Services.AddGrpc();
        await using var backend = builder.Build();
        backend.MapGrpcService<TestCatalog>();
        backend.MapGrpcService<TestAudit>();
        await backend.StartAsync();
        var address = backend.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();

        using var app = new Factory
        {
            Configure = o =>
            {
                o.Services["catalog"].Mode = "Real";
                o.Services["catalog"].Address = address;
                o.Services["catalog"].TimeoutSeconds = 1;
                o.Services["audit"].Mode = "Real";
                o.Services["audit"].Address = address;
            }
        };
        using var client = app.CreateClient();

        var real = await client.GetAsync("/gateway/api/v1/catalog/products");
        Assert.Equal(HttpStatusCode.OK, real.StatusCode);
        Assert.Equal("Real", real.Headers.GetValues("X-Gateway-Mode").Single());

        var json = await real.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        var productName = doc.RootElement.GetProperty("data").GetProperty("items")[0].GetProperty("name").GetString();
        Assert.Equal("real-product", productName);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/gateway/api/v1/catalog/products/11111111-1111-1111-1111-111111111111")).StatusCode);
        Assert.Equal(HttpStatusCode.GatewayTimeout, (await client.GetAsync("/gateway/api/v1/catalog/categories")).StatusCode);

        client.DefaultRequestHeaders.Authorization = new("Bearer", Factory.Token());
        Assert.Equal("Mock", (await client.GetAsync("/gateway/api/v1/commerce/cart")).Headers.GetValues("X-Gateway-Mode").Single());

        // Audit real gRPC
        client.DefaultRequestHeaders.Authorization = new("Bearer", Factory.TokenFor("ops", "staff"));
        var audit = await client.GetAsync("/gateway/api/v1/audit/logs");
        Assert.Equal(HttpStatusCode.OK, audit.StatusCode);
        Assert.Equal("Real", audit.Headers.GetValues("X-Gateway-Mode").Single());

        var auditJson = await audit.Content.ReadAsStringAsync();
        using var auditDoc = JsonDocument.Parse(auditJson);
        Assert.Equal("real-product-created", auditDoc.RootElement.GetProperty("data").GetProperty("items")[0].GetProperty("eventType").GetString());

        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/gateway/api/v1/audit/logs/{Guid.NewGuid()}")).StatusCode);

        var eventTypes = await client.GetAsync("/gateway/api/v1/audit/event-types");
        var eventTypesJson = await eventTypes.Content.ReadAsStringAsync();
        using var etDoc = JsonDocument.Parse(eventTypesJson);
        Assert.Equal("ProductCreated", etDoc.RootElement.GetProperty("data").GetProperty("items")[0].GetProperty("code").GetString());

        await backend.StopAsync();
        var unavailable = await client.GetAsync("/gateway/api/v1/catalog/products");
        Assert.Contains(unavailable.StatusCode, new[] { HttpStatusCode.ServiceUnavailable, HttpStatusCode.GatewayTimeout });
        Assert.Equal("Real", unavailable.Headers.GetValues("X-Gateway-Mode").Single());
    }

    [Fact]
    public void Production_rejects_mock_and_plaintext()
    {
        var options = new GatewayOptions { CorsOrigins = ["https://shop.example/"] };
        foreach (var name in GatewayOptions.CanonicalServices)
        {
            options.Services[name] = new() { Address = "https://service.example" };
        }
        Assert.True(options.IsValid(false));

        options.Services["catalog"].Mode = "Mock";
        Assert.False(options.IsValid(false));

        options.Services["catalog"].Mode = "Real";
        options.Services["catalog"].Address = "http://service.example";
        Assert.False(options.IsValid(false));
    }
}

public sealed class TestCatalog : CatalogApi.CatalogApiBase
{
    public override async Task<CategoryPage> ListCategories(PageRequest request, ServerCallContext context)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), context.CancellationToken);
        return new CategoryPage();
    }

    public override Task<ProductPage> ListProducts(ProductQuery request, ServerCallContext context) => Task.FromResult(
        new ProductPage
        {
            Items = { new Product { Id = Guid.NewGuid().ToString(), Name = "real-product" } },
            Total = 1
        });

    public override Task<Product> GetProduct(IdRequest request, ServerCallContext context) =>
        throw new RpcException(new Status(StatusCode.NotFound, "private backend message"));
}

public sealed class TestAudit : AuditApi.AuditApiBase
{
    public override Task<AuditLogPage> QueryAuditLogs(QueryAuditLogsRequest request, ServerCallContext context) =>
        Task.FromResult(new AuditLogPage
        {
            Items = { new AuditLog { Id = Guid.NewGuid().ToString(), EventType = "real-product-created", ServiceName = "catalog" } },
            Total = 1,
        });

    public override Task<AuditLog> GetAuditLog(IdRequest request, ServerCallContext context) =>
        throw new RpcException(new Status(StatusCode.NotFound, "private backend message"));

    public override Task<AuditEventTypeList> ListAuditEventTypes(Empty request, ServerCallContext context) =>
        Task.FromResult(new AuditEventTypeList
        {
            Items = { new AuditEventType { Code = "ProductCreated", IsEnabled = true } },
            Total = 1
        });

    public override Task<AuditEventType> SetAuditEventTypeEnabled(SetAuditEventTypeEnabledRequest request, ServerCallContext context) =>
        Task.FromResult(new AuditEventType { Code = request.Code, IsEnabled = request.Enabled });
}
