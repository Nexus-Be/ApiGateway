namespace Nexus.Gateway.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

public sealed class CatalogMockTests
{
    [Fact]
    public async Task ListProducts_ReturnsMockData_WithPagedEnvelope()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var response = await client.GetAsync("/api/v1/catalog/products?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(200, root.GetProperty("statusCode").GetInt32());

        var data = root.GetProperty("data");
        Assert.True(data.GetProperty("items").GetArrayLength() > 0);
        Assert.True(data.TryGetProperty("pagination", out _));
    }

    [Fact]
    public async Task GetProduct_ReturnsSuccess()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var response = await client.GetAsync("/api/v1/catalog/products/22222222-2222-2222-2222-000000000001");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(200, root.GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task ListCategories_ReturnsSuccess()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var response = await client.GetAsync("/api/v1/catalog/categories");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(200, root.GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task GetInventory_ReturnsSuccess()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var response = await client.GetAsync("/api/v1/catalog/inventory/22222222-2222-2222-2222-000000000001");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(200, root.GetProperty("statusCode").GetInt32());
    }
}
