using System.Net;
using System.Text.Json;
using Xunit;

namespace Nexus.Gateway.Tests;

public sealed class PaginationRequestBindingTests
{
    [Theory]
    [InlineData("/api/v1/catalog/categories", null)]
    [InlineData("/api/v1/catalog/products", null)]
    [InlineData("/api/v1/auctions", null)]
    [InlineData("/api/v1/audit/logs", "staff")]
    [InlineData("/api/v1/commerce/orders", "buyer")]
    [InlineData("/api/v1/seller/products", "seller")]
    [InlineData("/api/v1/seller/orders", "seller")]
    [InlineData("/api/v1/admin/users", "admin")]
    [InlineData("/api/v1/admin/orders", "admin")]
    [InlineData("/api/v1/wallet/transactions", "buyer")]
    public async Task Paged_action_keeps_flat_query_defaults_and_validates_bounds(string route, string? role)
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        if (role is not null)
            client.DefaultRequestHeaders.Authorization = new("Bearer", Factory.TokenFor("user-1", role));

        using var defaultResult = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, defaultResult.StatusCode);
        using var defaultBody = JsonDocument.Parse(await defaultResult.Content.ReadAsStringAsync());
        var defaults = defaultBody.RootElement.GetProperty("data").GetProperty("pagination");
        Assert.Equal(1, defaults.GetProperty("pageIndex").GetInt32());
        Assert.Equal(20, defaults.GetProperty("pageSize").GetInt32());

        using var customResult = await client.GetAsync(route + "?page=2&pageSize=7");
        Assert.Equal(HttpStatusCode.OK, customResult.StatusCode);
        using var customBody = JsonDocument.Parse(await customResult.Content.ReadAsStringAsync());
        var custom = customBody.RootElement.GetProperty("data").GetProperty("pagination");
        Assert.Equal(2, custom.GetProperty("pageIndex").GetInt32());
        Assert.Equal(7, custom.GetProperty("pageSize").GetInt32());

        using var invalidResult = await client.GetAsync(route + "?pageSize=101");
        Assert.Equal(HttpStatusCode.BadRequest, invalidResult.StatusCode);
        using var invalidBody = JsonDocument.Parse(await invalidResult.Content.ReadAsStringAsync());
        Assert.Equal("VAL_INVALID_INPUT", invalidBody.RootElement.GetProperty("errorCode").GetString());
    }
}
