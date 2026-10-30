using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Nexus.Gateway.Security;
using Nexus.Gateway.Services;
using Xunit;

namespace Nexus.Gateway.Tests;

public sealed class GatewayRequestResolutionTests
{
    [Fact]
    public void Resolution_prefers_routed_backend_over_url_inference()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        var options = app.Services.GetRequiredService<IOptionsMonitor<GatewayOptions>>();
        var runtime = app.Services.GetRequiredService<IGatewayRuntimeManager>();
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/v1/catalog/products";
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new BackendTag("users")), "collision"));

        var first = GatewayRequestResolution.Resolve(context, options, runtime);
        Assert.Equal("users", first.Service);
        Assert.Equal("users", GatewaySecurityHelper.DetectServiceForRequest(context));
        Assert.Equal(first, GatewayRequestResolution.Resolve(context, options, runtime));
        Assert.Equal(first.EffectiveMode(context, "catalog", options.CurrentValue, runtime),
            first.EffectiveMode(context, "catalog", options.CurrentValue, runtime));
        Assert.True(GatewaySecurityHelper.IsRequestInMockMode(context, options.CurrentValue, runtime));
    }

    [Theory]
    [InlineData("/api/v1/catalog/search")]
    [InlineData("/gateway/api/v1/catalog/search")]
    public async Task Unknown_route_passes_through_to_not_found(string path)
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("X-Service-Mode-catalog", "Real");
        request.Content = System.Net.Http.Json.JsonContent.Create(new { keyword = "keyboard" });
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Auth_bypass_and_service_mode_override_remain_independent()
    {
        using var app = new Factory { Configure = o => o.AuthMode = "Bypass" };
        using var client = app.CreateClient();
        using var mock = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, mock.StatusCode);

        using var realRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        realRequest.Headers.Add("X-Service-Mode-users", "Real");
        using var real = await client.SendAsync(realRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, real.StatusCode);

        using var control = await client.GetAsync("/api/v1/gateway/modes");
        Assert.Equal(HttpStatusCode.Unauthorized, control.StatusCode);
    }

    [Fact]
    public async Task Specific_service_override_wins_over_global_header_without_affecting_another_request()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Factory.Token());
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        request.Headers.Add("X-Mock-Mode", "Real");
        request.Headers.Add("X-Service-Mode-users", "Mock");
        using var first = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("Mock", first.Headers.GetValues("X-Gateway-Mode").Single());

        using var second = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("Mock", second.Headers.GetValues("X-Gateway-Mode").Single());
    }
}
