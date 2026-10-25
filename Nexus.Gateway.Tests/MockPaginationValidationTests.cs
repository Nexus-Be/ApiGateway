using System.Net;
using System.Text.Json;
using Xunit;

namespace Nexus.Gateway.Tests;

/// <summary>
/// Pagination validation on a paged mock route. These cases used to target the declarative
/// <c>Mocks/catalog-products.mock.json</c> fixture; that fixture (and with it the last declarative
/// paged mock in the repo) was removed when the catalog surface moved to a controller action +
/// <c>Seeds/catalog/products.json</c>, so the same contract is now asserted against the controller.
/// </summary>
public sealed class MockPaginationValidationTests
{
    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=101")]
    public async Task Paged_route_rejects_out_of_range_pagination(string query)
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        using var response = await client.GetAsync("/api/v1/products" + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("VAL_INVALID_INPUT", json.RootElement.GetProperty("errorCode").GetString());
    }

    [Theory]
    [InlineData("?page=abc")]
    [InlineData("?pageSize=abc")]
    public async Task Paged_route_rejects_non_numeric_pagination(string query)
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        using var response = await client.GetAsync("/api/v1/products" + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
