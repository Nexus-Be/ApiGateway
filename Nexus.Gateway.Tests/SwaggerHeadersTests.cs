namespace Nexus.Gateway.Tests;

using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Models;
using Nexus.Gateway.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

public class SwaggerHeadersTests : IClassFixture<Factory>
{
    private readonly Factory _factory;

    public SwaggerHeadersTests(Factory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void GatewayHeaderOperationFilter_AddsRequiredHeaders_WithDefaultsAndEnums()
    {
        // Arrange
        var filter = new GatewayHeaderOperationFilter();
        var operation = new OpenApiOperation();
        var apiDescription = new ApiDescription();
        var schemaRepository = new SchemaRepository();
        var context = new OperationFilterContext(apiDescription, new SchemaGenerator(new SchemaGeneratorOptions(), new JsonSerializerDataContractResolver(new JsonSerializerOptions())), schemaRepository, null!);

        // Act
        filter.Apply(operation, context);

        // Assert
        Assert.NotNull(operation.Parameters);
        Assert.Equal(3, operation.Parameters.Count);

        var modeParam = operation.Parameters.FirstOrDefault(p => p.Name == "X-Gateway-Mode");
        Assert.NotNull(modeParam);
        Assert.Equal(ParameterLocation.Header, modeParam.In);
        Assert.False(modeParam.Required);
        Assert.Equal("Mock", (modeParam.Schema.Default as Microsoft.OpenApi.Any.OpenApiString)?.Value);

        var authParam = operation.Parameters.FirstOrDefault(p => p.Name == "X-Gateway-Auth");
        Assert.NotNull(authParam);
        Assert.Equal(ParameterLocation.Header, authParam.In);
        Assert.False(authParam.Required);
        Assert.Equal("Bypass", (authParam.Schema.Default as Microsoft.OpenApi.Any.OpenApiString)?.Value);

        var roleParam = operation.Parameters.FirstOrDefault(p => p.Name == "X-Mock-Role");
        Assert.NotNull(roleParam);
        Assert.Equal(ParameterLocation.Header, roleParam.In);
        Assert.False(roleParam.Required);
        Assert.Equal("buyer", (roleParam.Schema.Default as Microsoft.OpenApi.Any.OpenApiString)?.Value);
    }

    [Fact]
    public async Task SwaggerJson_ContainsGatewayHeadersAndSecurityDefinitions()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Verify securitySchemes
        var components = root.GetProperty("components");
        var securitySchemes = components.GetProperty("securitySchemes");
        Assert.True(securitySchemes.TryGetProperty("Bearer", out _));
        Assert.True(securitySchemes.TryGetProperty("X-Gateway-Auth", out _));
        Assert.True(securitySchemes.TryGetProperty("X-Gateway-Mode", out _));
        Assert.True(securitySchemes.TryGetProperty("X-Mock-Role", out _));

        // Verify that endpoints carry the header parameters
        var paths = root.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/v1/cart", out var cartPath));
        var getOp = cartPath.GetProperty("get");
        var parameters = getOp.GetProperty("parameters");

        var paramNames = parameters.EnumerateArray()
            .Select(p => p.GetProperty("name").GetString())
            .ToList();

        Assert.Contains("X-Gateway-Mode", paramNames);
        Assert.Contains("X-Gateway-Auth", paramNames);
        Assert.Contains("X-Mock-Role", paramNames);
    }

    [Fact]
    public async Task Request_ReturnsGatewayModeAndAuthHeaders_InResponse()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act - Call with custom mode and auth headers
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/cart");
        request.Headers.Add("X-Gateway-Mode", "Mock");
        request.Headers.Add("X-Gateway-Auth", "Bypass");
        request.Headers.Add("X-Mock-Role", "buyer");

        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Gateway-Mode"));
        Assert.Equal("Mock", response.Headers.GetValues("X-Gateway-Mode").First());

        Assert.True(response.Headers.Contains("X-Gateway-Auth"));
        Assert.Equal("Bypass", response.Headers.GetValues("X-Gateway-Auth").First());

        Assert.True(response.Headers.Contains("X-Trace-Id"));
    }

    [Fact]
    public async Task SwaggerJson_UsersDoc_ContainsOnlyUsersEndpoints()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/swagger/users/swagger.json");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var paths = doc.RootElement.GetProperty("paths");

        // Users doc must contain auth / user endpoints
        Assert.True(paths.TryGetProperty("/api/v1/auth/login", out _) || paths.TryGetProperty("/api/v1/auth/register", out _));

        // Users doc must NOT contain cart or categories or wallet
        Assert.False(paths.TryGetProperty("/api/v1/cart", out _));
        Assert.False(paths.TryGetProperty("/api/v1/categories", out _));
        Assert.False(paths.TryGetProperty("/api/v1/wallets/me", out _));
    }

    [Fact]
    public async Task SwaggerJson_CatalogDoc_ContainsOnlyCatalogEndpoints()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/swagger/catalog/swagger.json");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var paths = doc.RootElement.GetProperty("paths");

        // Catalog doc must contain categories or products
        Assert.True(paths.TryGetProperty("/api/v1/categories", out _) || paths.TryGetProperty("/api/v1/products", out _));

        // Catalog doc must NOT contain auth login or cart
        Assert.False(paths.TryGetProperty("/api/v1/auth/login", out _));
        Assert.False(paths.TryGetProperty("/api/v1/cart", out _));
    }

    [Fact]
    public async Task SwaggerJson_WalletDoc_ContainsOnlyWalletEndpoints()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/swagger/wallet/swagger.json");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var paths = doc.RootElement.GetProperty("paths");

        // Wallet doc must contain wallet endpoints
        Assert.True(paths.TryGetProperty("/api/v1/wallets/me", out _));

        // Wallet doc must NOT contain categories or cart
        Assert.False(paths.TryGetProperty("/api/v1/categories", out _));
        Assert.False(paths.TryGetProperty("/api/v1/cart", out _));
    }

    [Fact]
    public async Task SwaggerDownstream_UnknownService_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/swagger-downstream/unknown_service/swagger.json");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SwaggerDownstream_OfflineService_ReturnsServiceUnavailable()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act - In local unit tests, downstream service on 8001 is offline
        var response = await client.GetAsync("/swagger-downstream/users/swagger.json");

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
