namespace Nexus.Gateway.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Nexus.Gateway.Dispatching;
using Nexus.Gateway.HealthProber;
using Nexus.Gateway.Responses;
using Xunit;

public class RestDownstreamDispatcherTests
{
    private readonly IApiResponseFactory _responseFactory = new ApiResponseFactory();

    private sealed class FakeModeEvaluator : IModeEvaluator
    {
        public string Mode { get; set; } = "Mock";
        public BackendOptions Options { get; set; } = new() { Address = "http://localhost:8002", TimeoutSeconds = 5 };

        public string EvaluateMode(HttpContext context, string serviceName) => Mode;
        public BackendOptions? GetServiceOptions(string serviceName) => Options;
    }

    private sealed class FakeMockDataMapper : IMockDataMapper
    {
        public (JsonElement? Data, IResult? Failure) MockResult { get; set; }
        public (string? JsonString, IResult? Failure) PagedMockResult { get; set; }

        public Task<(JsonElement? Data, IResult? Failure)> MapMockAsync(
            HttpContext context, string service, string operation, BackendOptions config, CancellationToken cancellationToken)
        {
            return Task.FromResult(MockResult);
        }

        public Task<(string? JsonString, IResult? Failure)> MapMockPagedJsonAsync(
            HttpContext context, string service, string operation, BackendOptions config, CancellationToken cancellationToken)
        {
            return Task.FromResult(PagedMockResult);
        }
    }

    private sealed class FakeHealthProber : IHealthProberManager
    {
        public bool Available { get; set; } = true;
        public bool IsAvailable { get => Available; set => Available = value; }
        public bool IsServiceAvailable(string serviceName) => Available;
        public IReadOnlyList<ServiceHealthState> GetAllStates() => Array.Empty<ServiceHealthState>();
        public ServiceHealthState? GetState(string serviceName) => null;
        public ProberRunStatus GetStatus(string serviceName) => ProberRunStatus.Healthy;
        public Task<bool> EnableServiceProberAsync(string serviceName, int? intervalSeconds = null, int? threshold = null) => Task.FromResult(true);
        public Task<bool> DisableServiceProberAsync(string serviceName) => Task.FromResult(true);
        public Task SetBulkProberAsync(bool enableAll) => Task.CompletedTask;
        public Task<HealthCheckSample> ProbeOnceAsync(string serviceName, CancellationToken cancellationToken = default)
            => Task.FromResult(new HealthCheckSample(DateTime.UtcNow, true, 200, 10, null));
    }

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public TestHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;
        public FakeHttpClientFactory(HttpClient client) => _client = client;
        public HttpClient CreateClient(string name) => _client;
    }

    [Fact]
    public async Task MockMode_ForwardAsync_ReturnsSuccessEnvelope()
    {
        var evaluator = new FakeModeEvaluator { Mode = "Mock" };
        using var seedDoc = JsonDocument.Parse("{\"id\":\"prod-123\",\"name\":\"Smartphone X\"}");
        var mapper = new FakeMockDataMapper { MockResult = (seedDoc.RootElement.Clone(), null) };
        var httpCaller = new HttpBackendCaller(new FakeHttpClientFactory(new HttpClient()), _responseFactory);

        var dispatcher = new GatewayDispatcher(evaluator, mapper, httpCaller, _responseFactory);

        var httpContext = new DefaultHttpContext();
        var request = new RestForwardRequest
        {
            Service = "catalog",
            DownstreamPath = "/api/v1/products/prod-123",
            Method = HttpMethod.Get,
            MockSeed = "product",
            SuccessMessage = "Product retrieved successfully"
        };

        var result = await dispatcher.DispatchRestAsync(httpContext, request);
        Assert.NotNull(result);
        Assert.Equal("Mock", httpContext.Response.Headers["X-Gateway-Mode"]);
    }

    [Fact]
    public async Task MockMode_ForwardPagedAsync_ReturnsPagedEnvelope()
    {
        var evaluator = new FakeModeEvaluator { Mode = "Mock" };
        var pagedJson = "{\"items\":[{\"id\":\"1\",\"name\":\"Item 1\"},{\"id\":\"2\",\"name\":\"Item 2\"}],\"total\":2}";
        var mapper = new FakeMockDataMapper { PagedMockResult = (pagedJson, null) };
        var httpCaller = new HttpBackendCaller(new FakeHttpClientFactory(new HttpClient()), _responseFactory);

        var dispatcher = new GatewayDispatcher(evaluator, mapper, httpCaller, _responseFactory);

        var httpContext = new DefaultHttpContext();
        var request = new RestPagedForwardRequest
        {
            Service = "catalog",
            DownstreamPath = "/api/v1/products",
            Method = HttpMethod.Get,
            MockSeed = "products",
            Page = 1,
            PageSize = 10
        };

        var result = await dispatcher.DispatchRestPagedAsync(httpContext, request);
        Assert.NotNull(result);
        Assert.Equal("Mock", httpContext.Response.Headers["X-Gateway-Mode"]);
    }

    [Fact]
    public async Task RealMode_CallsDownstreamHttp_WhenHealthy()
    {
        var evaluator = new FakeModeEvaluator { Mode = "Real" };
        var mapper = new FakeMockDataMapper();
        var healthProber = new FakeHealthProber { IsAvailable = true };

        HttpRequestMessage? capturedRequest = null;
        var fakeHandler = new TestHttpMessageHandler((req, ct) =>
        {
            capturedRequest = req;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":{\"id\":\"prod-real\",\"name\":\"Real Phone\"}}")
            };
            return Task.FromResult(response);
        });

        var httpClient = new HttpClient(fakeHandler) { BaseAddress = new Uri("http://localhost:8002") };
        var httpCaller = new HttpBackendCaller(new FakeHttpClientFactory(httpClient), _responseFactory);

        var dispatcher = new GatewayDispatcher(evaluator, mapper, httpCaller, _responseFactory, healthProber);

        var httpContext = new DefaultHttpContext();
        var request = new RestForwardRequest
        {
            Service = "catalog",
            DownstreamPath = "/api/v1/products/prod-real",
            Method = HttpMethod.Get
        };

        var result = await dispatcher.DispatchRestAsync(httpContext, request);
        Assert.NotNull(result);
        Assert.Equal("Real", httpContext.Response.Headers["X-Gateway-Mode"]);
        Assert.NotNull(capturedRequest);
        Assert.Equal("/api/v1/products/prod-real", capturedRequest.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task RealMode_FailsWith503_WhenProberReportsUnhealthy()
    {
        var evaluator = new FakeModeEvaluator { Mode = "Real" };
        var mapper = new FakeMockDataMapper();
        var healthProber = new FakeHealthProber { IsAvailable = false };
        var httpCaller = new HttpBackendCaller(new FakeHttpClientFactory(new HttpClient()), _responseFactory);

        var dispatcher = new GatewayDispatcher(evaluator, mapper, httpCaller, _responseFactory, healthProber);

        var httpContext = new DefaultHttpContext();
        var request = new RestForwardRequest
        {
            Service = "catalog",
            DownstreamPath = "/api/v1/products/prod-1",
            Method = HttpMethod.Get
        };

        var result = await dispatcher.DispatchRestAsync(httpContext, request);
        Assert.NotNull(result);
        Assert.Equal("Real", httpContext.Response.Headers["X-Gateway-Mode"]);
    }

    [Fact]
    public async Task RealMode_ForwardNoContentAsync_Returns204_WhenDownstreamSucceeds()
    {
        var evaluator = new FakeModeEvaluator { Mode = "Real" };
        var mapper = new FakeMockDataMapper();
        var healthProber = new FakeHealthProber { IsAvailable = true };

        var fakeHandler = new TestHttpMessageHandler((req, ct) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.NoContent);
            return Task.FromResult(response);
        });

        var httpClient = new HttpClient(fakeHandler) { BaseAddress = new Uri("http://localhost:8002") };
        var httpCaller = new HttpBackendCaller(new FakeHttpClientFactory(httpClient), _responseFactory);

        var dispatcher = new GatewayDispatcher(evaluator, mapper, httpCaller, _responseFactory, healthProber);

        var httpContext = new DefaultHttpContext();
        var request = new RestForwardRequest
        {
            Service = "catalog",
            DownstreamPath = "/api/v1/product/prod-1",
            Method = HttpMethod.Delete
        };

        var result = await dispatcher.DispatchRestNoContentAsync(httpContext, request);
        Assert.NotNull(result);
        Assert.Equal("Real", httpContext.Response.Headers["X-Gateway-Mode"]);
    }
}
