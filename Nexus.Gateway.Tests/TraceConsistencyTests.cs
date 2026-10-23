using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Nexus.Gateway.Responses;
using Xunit;

namespace Nexus.Gateway.Tests;

public sealed class TraceConsistencyTests
{
    [Fact]
    public void Request_trace_id_does_not_change_inside_child_activity()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "request-trace-id" };
        var before = ApiResponseFactory.ResolveTraceId(context);

        using var source = new ActivitySource("GatewayTraceConsistencyTests");
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == source.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);
        using var activity = source.StartActivity("backend.call");

        Assert.NotNull(activity);
        Assert.Equal(before, ApiResponseFactory.ResolveTraceId(context));
    }

    [Fact]
    public async Task Real_backend_failure_header_and_body_use_one_request_trace()
    {
        using var app = new Factory
        {
            Configure = options =>
            {
                options.Services["users"].Mode = "Real";
                options.Services["users"].Address = "http://127.0.0.1:1";
                options.Services["users"].TimeoutSeconds = 1;
            }
        };
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Factory.Token());

        using var response = await client.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(response.Headers.GetValues("X-Trace-Id").Single(), doc.RootElement.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task Mock_response_header_and_body_use_one_request_trace()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Factory.Token());

        using var response = await client.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(response.Headers.GetValues("X-Trace-Id").Single(), doc.RootElement.GetProperty("traceId").GetString());
    }
}
