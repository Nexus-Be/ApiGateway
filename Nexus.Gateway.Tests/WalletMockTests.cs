namespace Nexus.Gateway.Tests;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

public sealed class WalletMockTests
{
    private const string UserId = "11111111-1111-1111-1111-000000000001";
    private const string WalletId = "w1000000-0000-0000-0000-000000000001";
    private const string WithdrawalId = "wd-001";

    private static HttpClient CreateClient(Factory app, string role = "buyer")
    {
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor(UserId, role));
        return client;
    }

    [Fact]
    public async Task GetMyWallet_ReturnsSuccess()
    {
        using var app = new Factory();
        using var client = CreateClient(app);

        var response = await client.GetAsync("/api/v1/wallets/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Mock", response.Headers.GetValues("X-Gateway-Mode").Single());

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(200, root.GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task InitiateVnPayTopup_ReturnsCreated()
    {
        using var app = new Factory();
        using var client = CreateClient(app);

        var response = await client.PostAsJsonAsync("/api/v1/wallets/topups/vnpay", new
        {
            amount = 50000m,
            bankCode = "NCB"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(201, doc.RootElement.GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task InitiateVnPayTopup_InvalidAmount_ThrowsValidationException()
    {
        using var app = new Factory();
        using var client = CreateClient(app);

        var response = await client.PostAsJsonAsync("/api/v1/wallets/topups/vnpay", new
        {
            amount = 5000m // Minimum is 10,000 VND
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.Equal("VAL_INVALID_AMOUNT", root.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task ProcessVnPayIpn_Public_ReturnsRawVnPayResponse()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var response = await client.PostAsync("/api/v1/wallets/vnpay/ipn", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal("00", root.GetProperty("RspCode").GetString());
        Assert.Equal("Confirm success.", root.GetProperty("Message").GetString());
    }

    [Fact]
    public async Task DirectCheckout_ReturnsSuccess()
    {
        using var app = new Factory();
        using var client = CreateClient(app);

        var response = await client.PostAsJsonAsync("/api/v1/wallets/checkout/direct", new
        {
            orderId = "ord-001",
            amount = 100000m,
            note = "Thanh toan don hang"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RequestWithdrawal_Seller_ReturnsCreated()
    {
        using var app = new Factory();
        using var client = CreateClient(app, role: "seller");

        var response = await client.PostAsJsonAsync("/api/v1/wallets/withdrawals", new
        {
            amount = 200000m,
            bankCode = "MB",
            accountNumber = "0123456789",
            accountHolderName = "NGUYEN VAN A"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task ProcessWithdrawal_Admin_ReturnsSuccess()
    {
        using var app = new Factory();
        using var client = CreateClient(app, role: "admin");

        var response = await client.PatchAsJsonAsync($"/api/v1/admin/wallets/withdrawals/{WithdrawalId}", new
        {
            action = "APPROVE",
            adminNote = "Approved by admin"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProcessWithdrawal_Buyer_ReturnsForbidden()
    {
        using var app = new Factory();
        using var client = CreateClient(app, role: "buyer");

        var response = await client.PatchAsJsonAsync($"/api/v1/admin/wallets/withdrawals/{WithdrawalId}", new
        {
            action = "APPROVE"
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetTransactions_ReturnsSuccess()
    {
        using var app = new Factory();
        using var client = CreateClient(app);

        var response = await client.GetAsync("/api/v1/wallets/transactions?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task AdjustBalance_Admin_ReturnsOk()
    {
        using var app = new Factory();
        using var client = CreateClient(app, role: "admin");

        var response = await client.PostAsJsonAsync($"/api/v1/admin/wallets/{WalletId}/adjustments", new
        {
            amount = 50000m,
            adjustmentType = "CREDIT",
            reason = "Bonus refund"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
