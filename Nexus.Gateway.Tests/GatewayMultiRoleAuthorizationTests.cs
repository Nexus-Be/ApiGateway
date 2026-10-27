using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace Nexus.Gateway.Tests;

public sealed class GatewayMultiRoleAuthorizationTests
{
    private const string CategoryId = "c1000000-0000-0000-0000-000000000001";

    [Theory]
    [InlineData("seller", HttpStatusCode.Created)]
    [InlineData("staff", HttpStatusCode.Created)]
    [InlineData("admin", HttpStatusCode.Created)]
    public async Task CreateCategory_PermitsSellerStaffAndAdmin(string role, HttpStatusCode expectedStatus)
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor($"user-{role}", role));

        var response = await client.PostAsJsonAsync("/api/v1/categories", new
        {
            name = "Smart Home & IoT",
            slug = "smart-home-iot",
            description = "Devices for automated living"
        });

        Assert.Equal(expectedStatus, response.StatusCode);
    }

    [Fact]
    public async Task CreateCategory_RejectsBuyer()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor("buyer-1", "buyer"));

        var response = await client.PostAsJsonAsync("/api/v1/categories", new
        {
            name = "Forbidden Category",
            slug = "forbidden-category"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("staff", HttpStatusCode.NoContent)]
    [InlineData("admin", HttpStatusCode.NoContent)]
    public async Task DeleteCategory_PermitsStaffAndAdmin(string role, HttpStatusCode expectedStatus)
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor($"user-{role}", role));

        var response = await client.DeleteAsync($"/api/v1/categories/{CategoryId}");
        Assert.Equal(expectedStatus, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCategory_RejectsAnonymous()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var response = await client.DeleteAsync($"/api/v1/categories/{CategoryId}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("seller", HttpStatusCode.Created)]
    [InlineData("staff", HttpStatusCode.Created)]
    [InlineData("admin", HttpStatusCode.Created)]
    public async Task CreateProduct_PermitsSellerStaffAndAdmin(string role, HttpStatusCode expectedStatus)
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor($"user-{role}", role));

        var response = await client.PostAsJsonAsync("/api/v1/products", new
        {
            categoryId = CategoryId,
            name = "Multi-Role Ergonomic Chair",
            description = "High quality ergonomic mesh chair.",
            brand = "NexusErgo",
            variants = new[]
            {
                new
                {
                    skuCode = "NX-CHAIR-001",
                    name = "Midnight Black",
                    price = 3500000,
                    currency = "VND",
                    isActive = true
                }
            }
        });

        Assert.Equal(expectedStatus, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_RejectsBuyer()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor("buyer-1", "buyer"));

        var response = await client.PostAsJsonAsync("/api/v1/products", new
        {
            categoryId = CategoryId,
            name = "Chair",
            variants = new[]
            {
                new { skuCode = "SKU-1", name = "V1", price = 100000, currency = "VND", isActive = true }
            }
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("buyer", HttpStatusCode.Created)]
    [InlineData("seller", HttpStatusCode.Created)]
    [InlineData("staff", HttpStatusCode.Created)]
    [InlineData("admin", HttpStatusCode.Created)]
    public async Task WalletWithdrawal_PermitsAnyAuthenticatedUserWithWallet(string role, HttpStatusCode expectedStatus)
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor($"user-{role}", role));

        var response = await client.PostAsJsonAsync("/api/v1/wallets/withdrawals", new
        {
            amount = 150000m,
            bankCode = "VCB",
            accountNumber = "9876543210",
            accountHolderName = "NGUYEN VAN A"
        });

        Assert.Equal(expectedStatus, response.StatusCode);
    }

    [Theory]
    [InlineData("staff", HttpStatusCode.OK)]
    [InlineData("admin", HttpStatusCode.OK)]
    public async Task WalletReviewWithdrawal_PermitsStaffAndAdmin(string role, HttpStatusCode expectedStatus)
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor($"user-{role}", role));

        var response = await client.PatchAsJsonAsync("/api/v1/admin/wallets/withdrawals/w1000000-0000-0000-0000-000000000001", new
        {
            action = "APPROVE",
            reason = "Valid financial withdrawal documentation provided."
        });

        Assert.Equal(expectedStatus, response.StatusCode);
    }

    [Fact]
    public async Task WalletReviewWithdrawal_RejectsBuyerAndSeller()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor("seller-1", "seller"));

        var response = await client.PatchAsJsonAsync("/api/v1/admin/wallets/withdrawals/w1000000-0000-0000-0000-000000000001", new
        {
            action = "APPROVE",
            reason = "Seller trying to self-approve."
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
