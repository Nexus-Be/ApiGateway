namespace Nexus.Gateway.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
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

        public Task<(JsonElement? Data, IResult? Failure)> MapMockAsync<T>(
            HttpContext context, string service, string operation, BackendOptions config, MessageParser<T>? parser, CancellationToken cancellationToken) where T : class, IMessage<T>
        {
            return Task.FromResult(MockResult);
        }

        public Task<(string? JsonString, IResult? Failure)> MapMockPagedJsonAsync(
            HttpContext context, string service, string operation, BackendOptions config, CancellationToken cancellationToken)
        {
            return Task.FromResult(PagedMockResult);
        }
    }

    private sealed class FakeGrpcServiceCaller : IGrpcServiceCaller
    {
        public Task<JsonElement> CallAsync<T>(HttpContext context, Func<CallOptions, AsyncUnaryCall<T>> realCall, BackendOptions config, CancellationToken cancellationToken) where T : class, IMessage<T>
            => throw new NotImplementedException();

        public Task<string> CallPagedJsonAsync<T>(HttpContext context, Func<CallOptions, AsyncUnaryCall<T>> realCall, BackendOptions config, CancellationToken cancellationToken) where T : class, IMessage<T>
            => throw new NotImplementedException();
    }

    private sealed class FakeHealthProber : IHealthProberManager
    {
        public bool Available { get; set; } = true;
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
        var grpc = new FakeGrpcServiceCaller();
        var httpCaller = new HttpBackendCaller(new FakeHttpClientFactory(new HttpClient()), _responseFactory);

        var dispatcher = new GatewayDispatcher(evaluator, mapper, grpc, httpCaller, _responseFactory);

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
        var grpc = new FakeGrpcServiceCaller();
        var httpCaller = new HttpBackendCaller(new FakeHttpClientFactory(new HttpClient()), _responseFactory);

        var dispatcher = new GatewayDispatcher(evaluator, mapper, grpc, httpCaller, _responseFactory);

        var httpContext = new DefaultHttpContext();
        var request = new RestPagedForwardRequest
        {
            Service = "catalog",
            DownstreamPath = "/api/v1/products",
            Method = HttpMethod.Get,
            MockSeed = "products",
            Page = 1,
            PageSize = 20
        };

        var result = await dispatcher.DispatchRestPagedAsync(httpContext, request);
        Assert.NotNull(result);
        Assert.Equal("Mock", httpContext.Response.Headers["X-Gateway-Mode"]);
    }

    [Fact]
    public async Task RealMode_UnhealthyService_Returns503DownstreamUnhealthy()
    {
        var evaluator = new FakeModeEvaluator { Mode = "Real" };
        var mapper = new FakeMockDataMapper();
        var grpc = new FakeGrpcServiceCaller();
        var prober = new FakeHealthProber { Available = false };
        var httpCaller = new HttpBackendCaller(new FakeHttpClientFactory(new HttpClient()), _responseFactory);

        var dispatcher = new GatewayDispatcher(evaluator, mapper, grpc, httpCaller, _responseFactory, prober);

        var httpContext = new DefaultHttpContext();
        var request = new RestForwardRequest
        {
            Service = "catalog",
            DownstreamPath = "/api/v1/products",
            Method = HttpMethod.Get
        };

        var result = await dispatcher.DispatchRestAsync(httpContext, request);
        Assert.NotNull(result);
        Assert.Equal("Real", httpContext.Response.Headers["X-Gateway-Mode"]);
    }

    [Fact]
    public async Task RealMode_HttpBackendCaller_InvokesDownstreamAndForwardsHeaders()
    {
        var handler = new TestHttpMessageHandler((req, _) =>
        {
            Assert.Equal("Bearer test-token", req.Headers.Authorization?.ToString());
            Assert.Equal("user-456", req.Headers.GetValues("x-user-id").First());
            Assert.Equal("trace-999", req.Headers.GetValues("x-trace-id").First());
            Assert.Equal("POST", req.Method.Method);

            var resp = new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("{\"productId\":\"p100\",\"status\":\"CREATED\"}", System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(resp);
        });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:8002")
        };

        var httpCaller = new HttpBackendCaller(new FakeHttpClientFactory(httpClient), _responseFactory);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = "Bearer test-token";
        httpContext.Request.Headers["x-user-id"] = "user-456";
        httpContext.Request.Headers["x-trace-id"] = "trace-999";

        var request = new RestForwardRequest
        {
            Service = "catalog",
            DownstreamPath = "/api/v1/products",
            Method = HttpMethod.Post,
            Body = new { name = "New Product", price = 100000 },
            SuccessStatusCode = StatusCodes.Status201Created
        };

        var options = new BackendOptions { Address = "http://localhost:8002", TimeoutSeconds = 10 };
        var (data, failure) = await httpCaller.CallAsync(httpContext, "catalog", request, options, CancellationToken.None);

        Assert.Null(failure);
        Assert.NotNull(data);
        Assert.Equal("p100", data.Value.GetProperty("productId").GetString());
    }

    [Fact]
    public async Task RealMode_HttpBackendCaller_PropagatesDownstreamError()
    {
        var handler = new TestHttpMessageHandler((_, _) =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"success\":false,\"code\":400,\"message\":\"Invalid product name\",\"error\":{\"code\":\"VAL_INVALID_INPUT\"}}", System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(resp);
        });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:8002")
        };

        var httpCaller = new HttpBackendCaller(new FakeHttpClientFactory(httpClient), _responseFactory);

        var httpContext = new DefaultHttpContext();
        var request = new RestForwardRequest
        {
            Service = "catalog",
            DownstreamPath = "/api/v1/products",
            Method = HttpMethod.Post,
            Body = new { }
        };

        var options = new BackendOptions { Address = "http://localhost:8002", TimeoutSeconds = 10 };
        var (data, failure) = await httpCaller.CallAsync(httpContext, "catalog", request, options, CancellationToken.None);

        Assert.Null(data);
        Assert.NotNull(failure);
    }
}
