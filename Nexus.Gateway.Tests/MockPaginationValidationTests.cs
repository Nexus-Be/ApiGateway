using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Nexus.Gateway.Tests;

public sealed class MockPaginationValidationTests
{
    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?page=abc")]
    [InlineData("?pageSize=abc")]
    public async Task Declarative_paged_mock_rejects_invalid_pagination(string query)
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/catalog/search" + query, new { keyword = "keyboard" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("VAL_INVALID_INPUT", json.RootElement.GetProperty("errorCode").GetString());
    }
}
