using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Nexus.Gateway.Tests;

public sealed class BulkModesAtomicityTests
{
    [Theory]
    [InlineData(false, "INVALID_SERVICE_MODE")]
    [InlineData(true, "INVALID_AUTH_MODE")]
    public async Task Invalid_bulk_update_leaves_auth_and_service_modes_unchanged(bool invalidAuth, string expectedError)
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Factory.TokenFor("root", "admin"));

        const string path = "/gateway/api/v1/gateway/modes";
        using var beforeResponse = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, beforeResponse.StatusCode);
        using var before = JsonDocument.Parse(await beforeResponse.Content.ReadAsStringAsync());
        var originalAuth = before.RootElement.GetProperty("data").GetProperty("gatewayMode").GetString();
        var originalCatalog = before.RootElement.GetProperty("data").GetProperty("services").GetProperty("catalog").GetProperty("mode").GetString();

        var changedAuth = originalAuth == "Bypass" ? "Enforce" : "Bypass";
        var changedCatalog = originalCatalog == "Mock" ? "Real" : "Mock";
        using var update = await client.PutAsJsonAsync(path, new
        {
            authMode = invalidAuth ? "not-an-auth-mode" : changedAuth,
            services = new Dictionary<string, string>
            {
                ["catalog"] = invalidAuth ? changedCatalog : "not-a-service-mode"
            }
        });
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
        using var error = JsonDocument.Parse(await update.Content.ReadAsStringAsync());
        Assert.Equal(expectedError, error.RootElement.GetProperty("errorCode").GetString());

        using var afterResponse = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, afterResponse.StatusCode);
        using var after = JsonDocument.Parse(await afterResponse.Content.ReadAsStringAsync());
        Assert.Equal(originalAuth, after.RootElement.GetProperty("data").GetProperty("gatewayMode").GetString());
        Assert.Equal(originalCatalog, after.RootElement.GetProperty("data").GetProperty("services").GetProperty("catalog").GetProperty("mode").GetString());
    }
}
