using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Nexus.Gateway.Tests;

public sealed class GatewayInputExceptionTests
{
    [Theory]
    [InlineData("register", "Username, valid email, and password with at least 8 characters are required.")]
    [InlineData("login", "Username and password are required.")]
    public async Task Typed_input_error_maps_to_existing_gateway_error_contract(string endpoint, string message)
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/v1/auth/" + endpoint, new
        {
            username = "",
            email = "invalid",
            password = ""
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.Equal("VAL_INVALID_INPUT", root.GetProperty("errorCode").GetString());
        Assert.Equal(message, root.GetProperty("detail").GetString());
        Assert.Equal(message, root.GetProperty("errors").GetProperty("General")[0].GetString());
        Assert.Equal(response.Headers.GetValues("X-Trace-Id").Single(), root.GetProperty("traceId").GetString());
    }
}
