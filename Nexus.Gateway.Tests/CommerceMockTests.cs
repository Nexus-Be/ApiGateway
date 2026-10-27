namespace Nexus.Gateway.Tests;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

public sealed class CommerceMockTests
{
    private const string UserId = "11111111-1111-1111-1111-000000000001";
    private const string ProductId = "b1000000-0000-0000-0000-000000000001";
    private const string SkuId = "c1000000-0000-0000-0000-000000000001";
    private const string CartItemId = "item-001";
    private const string OrderId = "ord-001";
    private const string SubOrderId = "sub-001";
    private const string QuoteId = "quote-001";

    private static HttpClient CreateClient(Factory app, string role = "buyer")
    {
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor(UserId, role));
        return client;
    }

    [Fact]
    public async Task GetCart_ReturnsSuccess()
    {
        using var app = new Factory();
        using var client = CreateClient(app);

        var response = await client.GetAsync("/api/v1/cart");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Mock", response.Headers.GetValues("X-Gateway-Mode").Single());

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(200, root.GetProperty("statusCode").GetInt32());
        
        // Assert camelCase schema in mock response data
        var data = root.GetProperty("data");
        Assert.True(data.TryGetProperty("cartId", out _));
        Assert.True(data.TryGetProperty("status", out var statusProp));
        Assert.Equal("ACTIVE", statusProp.GetString());
        Assert.True(data.TryGetProperty("itemCount", out _));
        Assert.True(data.TryGetProperty("subtotalAmount", out _));
        Assert.True(data.TryGetProperty("groups", out var groupsProp));
        Assert.Equal(JsonValueKind.Array, groupsProp.ValueKind);
    }

    [Fact]
    public async Task AddCartItem_ReturnsSuccess()
    {
        using var app = new Factory();
        using var client = CreateClient(app);

        var response = await client.PostAsJsonAsync("/api/v1/cart/items", new
        {
            productId = ProductId,
            skuId = SkuId,
            quantity = 2
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task AddCartItem_InvalidQuantity_ThrowsValidationException()
    {
        using var app = new Factory();
        using var client = CreateClient(app);

        var response = await client.PostAsJsonAsync("/api/v1/cart/items", new
        {
            productId = ProductId,
            skuId = SkuId,
            quantity = 0
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.Equal("VAL_INVALID_INPUT", root.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task UpdateCartItem_ReturnsSuccess()
    {
        using var app = new Factory();
        using var client = CreateClient(app);

        var response = await client.PatchAsJsonAsync($"/api/v1/cart/items/{CartItemId}", new
        {
            quantity = 5
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task RemoveCartItems_ReturnsSuccess()
    {
        using var app = new Factory();
        using var client = CreateClient(app);

        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/cart/items")
        {
            Content = JsonContent.Create(new
            {
                cartItemIds = new[] { CartItemId }
            })
        };
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ClearCart_ReturnsSuccess()
    {
        using var app = new Factory();
        using var client = CreateClient(app);

        var response = await client.DeleteAsync("/api/v1/cart/items/all");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SelectCartItems_ReturnsSuccess()
    {
        using var app = new Factory();
        using var client = CreateClient(app);

        var response = await client.GetAsync($"/api/v1/cart/items/select?cartItemIds={CartItemId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CheckoutQuote_ReturnsSuccess()
    {
        using var app = new Factory();
        using var client = CreateClient(app);

        var response = await client.PostAsJsonAsync("/api/v1/checkout/quote", new
        {
            cartItemIds = new[] { CartItemId },
            shippingAddress = new
            {
                receiverName = "Nguyen Van A",
                phoneNumber = "0901234567",
                street = "123 Le Loi",
                district = "Quan 1",
                province = "TP HCM"
            },
            contactPhone = "0901234567"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        var data = root.GetProperty("data");
        Assert.True(data.TryGetProperty("orders", out var ordersProp));
        Assert.Equal(JsonValueKind.Array, ordersProp.ValueKind);
        Assert.True(data.TryGetProperty("grandTotal", out _));
    }

    [Fact]
    public async Task CreateOrder_ReturnsCreated()
    {
        using var app = new Factory();
        using var client = CreateClient(app);

        var response = await client.PostAsJsonAsync("/api/v1/orders", new
        {
            quoteId = QuoteId,
            shippingAddress = "123 Le Loi, Quan 1, TP HCM",
            contactPhone = "0901234567",
            paymentMethod = "WALLET"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(201, doc.RootElement.GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task GetOrder_ReturnsSuccess()
    {
        using var app = new Factory();
        using var client = CreateClient(app);

        var response = await client.GetAsync($"/api/v1/order/{OrderId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSubOrder_ReturnsSuccess()
    {
        using var app = new Factory();
        using var client = CreateClient(app);

        var response = await client.GetAsync($"/api/v1/subOrder/{SubOrderId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CancelOrder_ReturnsSuccess()
    {
        using var app = new Factory();
        using var client = CreateClient(app);

        var response = await client.PostAsJsonAsync($"/api/v1/orders/{OrderId}/cancel", new
        {
            reason = "Buyer changed mind"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AcceptOrder_Seller_ReturnsSuccess()
    {
        using var app = new Factory();
        using var client = CreateClient(app, role: "seller");

        var response = await client.PostAsJsonAsync($"/api/v1/orders/{OrderId}/accept", new
        {
            subOrderId = SubOrderId
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RejectOrder_Seller_ReturnsSuccess()
    {
        using var app = new Factory();
        using var client = CreateClient(app, role: "seller");

        var response = await client.PostAsJsonAsync($"/api/v1/orders/{OrderId}/reject", new
        {
            subOrderId = SubOrderId,
            reasonCode = "OUT_OF_STOCK"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PaymentWebhook_ReturnsSuccess()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/payments/webhook", new
        {
            orderId = "6a1b2c3d-7777-4e8f-9a0b-1c2d3e4f5a07",
            transactionId = "TXN-9999",
            amount = 480000,
            status = "SUCCESS",
            paymentMethod = "VNPAY"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(200, root.GetProperty("statusCode").GetInt32());
        Assert.True(root.GetProperty("data").GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task CreateProductReview_ReturnsCreated()
    {
        using var app = new Factory();
        using var client = CreateClient(app, role: "buyer");

        var response = await client.PostAsJsonAsync($"/api/v1/order/{OrderId}/products/{ProductId}/reviews", new
        {
            rating = 5,
            comment = "San pham rat tot, dong goi ky."
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(201, root.GetProperty("statusCode").GetInt32());
        Assert.True(root.GetProperty("data").TryGetProperty("reviewId", out _));
    }

    [Fact]
    public async Task CreateUserFeedback_ReturnsCreated()
    {
        using var app = new Factory();
        using var client = CreateClient(app, role: "buyer");

        var response = await client.PostAsJsonAsync($"/api/v1/order/{OrderId}/user/{UserId}", new
        {
            rating = 5,
            feedback = "Seller giao hang nhanh chong."
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(201, root.GetProperty("statusCode").GetInt32());
        Assert.True(root.GetProperty("data").TryGetProperty("feedbackId", out _));
    }

    [Fact]
    public async Task UpdateProductReview_ReturnsSuccess()
    {
        using var app = new Factory();
        using var client = CreateClient(app, role: "buyer");

        var response = await client.PatchAsJsonAsync($"/api/v1/order/{OrderId}/products/{ProductId}/reviews", new
        {
            rating = 4,
            comment = "Cap nhat: San pham dung tot sau 1 tuan."
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(200, root.GetProperty("statusCode").GetInt32());
        Assert.Equal(4, root.GetProperty("data").GetProperty("rating").GetInt32());
    }

    [Fact]
    public async Task UpdateUserFeedback_ReturnsSuccess()
    {
        using var app = new Factory();
        using var client = CreateClient(app, role: "buyer");

        var response = await client.PatchAsJsonAsync($"/api/v1/order/{OrderId}/user/{UserId}", new
        {
            rating = 4,
            feedback = "Cap nhat feedback."
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(200, root.GetProperty("statusCode").GetInt32());
        Assert.Equal(4, root.GetProperty("data").GetProperty("rating").GetInt32());
    }

    [Fact]
    public async Task RefundSubOrder_ReturnsSuccess()
    {
        using var app = new Factory();
        using var client = CreateClient(app, role: "buyer");

        var response = await client.PostAsJsonAsync($"/api/v1/subOrder/{SubOrderId}/refund", new
        {
            reason = "Damaged item received",
            note = "Box was torn"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(200, root.GetProperty("statusCode").GetInt32());
        Assert.Equal("REFUNDED", root.GetProperty("data").GetProperty("status").GetString());
    }
}
