namespace Nexus.Gateway.Dispatching;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Nexus.Gateway.HealthProber;
using Nexus.Gateway.Responses;

public interface IGatewayDispatcher
{
    // Legacy Protobuf methods (kept for backward compatibility during migration)
    Task<IResult> DispatchAsync<T>(
        HttpContext context,
        string service,
        string operation,
        Func<CallOptions, AsyncUnaryCall<T>> realCall,
        MessageParser<T> parser,
        string? successMessage = null,
        int successStatusCode = StatusCodes.Status200OK) where T : class, IMessage<T>;

    Task<IResult> DispatchPagedAsync<T>(
        HttpContext context,
        string service,
        string operation,
        int pageIndex,
        int pageSize,
        Func<CallOptions, AsyncUnaryCall<T>> realCall,
        MessageParser<T> parser,
        int successStatusCode = StatusCodes.Status200OK) where T : class, IMessage<T>;

    Task<IResult> DispatchNoContentAsync<T>(
        HttpContext context,
        string service,
        string operation,
        Func<CallOptions, AsyncUnaryCall<T>> realCall,
        MessageParser<T> parser) where T : class, IMessage<T>;

    // REST Downstream methods
    Task<IResult> DispatchRestAsync(HttpContext context, RestForwardRequest request);
    Task<IResult> DispatchRestPagedAsync(HttpContext context, RestPagedForwardRequest request);
    Task<IResult> DispatchRestNoContentAsync(HttpContext context, RestForwardRequest request);
}

public sealed class GatewayDispatcher : IGatewayDispatcher
{
    public const string ActivityName = "Nexus.Gateway.Backend";
    private static readonly ActivitySource Activities = new(ActivityName);

    private readonly IModeEvaluator _modeEvaluator;
    private readonly IMockDataMapper _mockDataMapper;
    private readonly IGrpcServiceCaller _grpcServiceCaller;
    private readonly IHttpBackendCaller _httpBackendCaller;
    private readonly IApiResponseFactory _responseFactory;
    private readonly IHealthProberManager? _healthProberManager;

    public GatewayDispatcher(
        IModeEvaluator modeEvaluator,
        IMockDataMapper mockDataMapper,
        IGrpcServiceCaller grpcServiceCaller,
        IHttpBackendCaller httpBackendCaller,
        IApiResponseFactory responseFactory,
        IHealthProberManager? healthProberManager = null)
    {
        _modeEvaluator = modeEvaluator;
        _mockDataMapper = mockDataMapper;
        _grpcServiceCaller = grpcServiceCaller;
        _httpBackendCaller = httpBackendCaller;
        _responseFactory = responseFactory;
        _healthProberManager = healthProberManager;
    }

    #region REST Downstream Dispatching

    public async Task<IResult> DispatchRestAsync(HttpContext context, RestForwardRequest request)
    {
        var service = request.Service;
        var operation = request.MockSeed ?? request.DownstreamPath.Trim('/').Replace('/', '-');

        var config = _modeEvaluator.GetServiceOptions(service);
        if (config == null)
        {
            return _responseFactory.FailureResult(
                context,
                title: "Unknown backend service",
                status: StatusCodes.Status503ServiceUnavailable,
                detail: $"Service configuration for '{service}' was not found.",
                errorCode: "SERVICE_CONFIG_MISSING"
            );
        }

        var effectiveMode = _modeEvaluator.EvaluateMode(context, service);
        context.Items["GatewayMode"] = effectiveMode;
        using var activity = Activities.StartActivity($"{service}.{operation}");
        activity?.SetTag("backend.service", service);
        activity?.SetTag("backend.mode", effectiveMode);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));

        // 1. MOCK Mode
        if (effectiveMode == "Mock")
        {
            context.Response.Headers["X-Gateway-Mode"] = "Mock";
            context.Response.Headers.CacheControl = "no-store";

            var (dataElement, failure) = await _mockDataMapper.MapMockAsync<Google.Protobuf.WellKnownTypes.Struct>(
                context, service, operation, config, null, timeout.Token);
            if (failure != null) return failure;

            return _responseFactory.SuccessResult(context, dataElement, request.SuccessMessage, request.SuccessStatusCode);
        }

        // 2. REAL Mode (HTTP REST Downstream)
        context.Response.Headers["X-Gateway-Mode"] = "Real";

        if (_healthProberManager != null && !_healthProberManager.IsServiceAvailable(service))
        {
            return _responseFactory.FailureResult(
                context,
                title: "Downstream Service Unavailable",
                status: StatusCodes.Status503ServiceUnavailable,
                detail: $"Service '{service}' is currently unhealthy or undergoing recovery.",
                errorCode: "DOWNSTREAM_UNHEALTHY"
            );
        }

        var (realData, realFailure) = await _httpBackendCaller.CallAsync(context, service, request, config, timeout.Token);
        if (realFailure != null) return realFailure;

        return _responseFactory.SuccessResult(context, realData, request.SuccessMessage, request.SuccessStatusCode);
    }

    public async Task<IResult> DispatchRestPagedAsync(HttpContext context, RestPagedForwardRequest request)
    {
        var service = request.Service;
        var operation = request.MockSeed ?? request.DownstreamPath.Trim('/').Replace('/', '-');

        var config = _modeEvaluator.GetServiceOptions(service);
        if (config == null)
        {
            return _responseFactory.FailureResult(
                context,
                title: "Unknown backend service",
                status: StatusCodes.Status503ServiceUnavailable,
                detail: $"Service configuration for '{service}' was not found.",
                errorCode: "SERVICE_CONFIG_MISSING"
            );
        }

        var effectiveMode = _modeEvaluator.EvaluateMode(context, service);
        context.Items["GatewayMode"] = effectiveMode;
        using var activity = Activities.StartActivity($"{service}.{operation}");
        activity?.SetTag("backend.service", service);
        activity?.SetTag("backend.mode", effectiveMode);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));

        string jsonResult;

        if (effectiveMode == "Mock")
        {
            context.Response.Headers["X-Gateway-Mode"] = "Mock";
            context.Response.Headers.CacheControl = "no-store";

            var (mockJson, failure) = await _mockDataMapper.MapMockPagedJsonAsync(context, service, operation, config, timeout.Token);
            if (failure != null) return failure;

            jsonResult = mockJson!;
        }
        else
        {
            context.Response.Headers["X-Gateway-Mode"] = "Real";

            if (_healthProberManager != null && !_healthProberManager.IsServiceAvailable(service))
            {
                return _responseFactory.FailureResult(
                    context,
                    title: "Downstream Service Unavailable",
                    status: StatusCodes.Status503ServiceUnavailable,
                    detail: $"Service '{service}' is currently unhealthy or undergoing recovery.",
                    errorCode: "DOWNSTREAM_UNHEALTHY"
                );
            }

            var (realJson, realFailure) = await _httpBackendCaller.CallRawJsonAsync(
                context, service, request.DownstreamPath, request.Method, request.Body, request.QueryParams, config, timeout.Token);
            if (realFailure != null) return realFailure;

            jsonResult = realJson ?? "{}";
        }

        using var doc = JsonDocument.Parse(jsonResult);
        var itemsList = new List<JsonElement>();
        var totalCount = 0;

        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("data", out var dataElem) &&
            dataElem.ValueKind == JsonValueKind.Object)
        {
            root = dataElem;
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("items", out var itemsElem) && itemsElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in itemsElem.EnumerateArray())
                {
                    itemsList.Add(item.Clone());
                }
            }

            if (root.TryGetProperty("total", out var totalElem) && totalElem.TryGetInt32(out var t))
            {
                totalCount = t;
            }
            else if (root.TryGetProperty("totalCount", out var totalCountElem) && totalCountElem.TryGetInt32(out var tc))
            {
                totalCount = tc;
            }
            else if (root.TryGetProperty("pagination", out var paginationElem) &&
                     paginationElem.TryGetProperty("totalCount", out var pTotalElem) &&
                     pTotalElem.TryGetInt32(out var pt))
            {
                totalCount = pt;
            }
            else
            {
                totalCount = itemsList.Count;
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                itemsList.Add(item.Clone());
            }
            totalCount = itemsList.Count;
        }

        return _responseFactory.PagedResult(context, itemsList, request.Page, request.PageSize, totalCount, request.SuccessStatusCode);
    }

    public async Task<IResult> DispatchRestNoContentAsync(HttpContext context, RestForwardRequest request)
    {
        var service = request.Service;
        var operation = request.MockSeed ?? request.DownstreamPath.Trim('/').Replace('/', '-');

        var config = _modeEvaluator.GetServiceOptions(service);
        if (config == null)
        {
            return _responseFactory.FailureResult(
                context,
                title: "Unknown backend service",
                status: StatusCodes.Status503ServiceUnavailable,
                detail: $"Service configuration for '{service}' was not found.",
                errorCode: "SERVICE_CONFIG_MISSING"
            );
        }

        var effectiveMode = _modeEvaluator.EvaluateMode(context, service);
        context.Items["GatewayMode"] = effectiveMode;
        using var activity = Activities.StartActivity($"{service}.{operation}");
        activity?.SetTag("backend.service", service);
        activity?.SetTag("backend.mode", effectiveMode);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));

        if (effectiveMode == "Mock")
        {
            context.Response.Headers["X-Gateway-Mode"] = "Mock";
            context.Response.Headers.CacheControl = "no-store";

            var (_, failure) = await _mockDataMapper.MapMockAsync<Google.Protobuf.WellKnownTypes.Struct>(
                context, service, operation, config, null, timeout.Token);
            if (failure != null) return failure;

            return Results.NoContent();
        }

        context.Response.Headers["X-Gateway-Mode"] = "Real";

        if (_healthProberManager != null && !_healthProberManager.IsServiceAvailable(service))
        {
            return _responseFactory.FailureResult(
                context,
                title: "Downstream Service Unavailable",
                status: StatusCodes.Status503ServiceUnavailable,
                detail: $"Service '{service}' is currently unhealthy or undergoing recovery.",
                errorCode: "DOWNSTREAM_UNHEALTHY"
            );
        }

        var (_, realFailure) = await _httpBackendCaller.CallAsync(context, service, request, config, timeout.Token);
        if (realFailure != null) return realFailure;

        return Results.NoContent();
    }

    #endregion

    #region Legacy Protobuf Dispatching

    public async Task<IResult> DispatchAsync<T>(
        HttpContext context,
        string service,
        string operation,
        Func<CallOptions, AsyncUnaryCall<T>> realCall,
        MessageParser<T> parser,
        string? successMessage = null,
        int successStatusCode = StatusCodes.Status200OK) where T : class, IMessage<T>
    {
        var config = _modeEvaluator.GetServiceOptions(service);
        if (config == null)
        {
            return _responseFactory.FailureResult(
                context,
                title: "Unknown backend service",
                status: StatusCodes.Status503ServiceUnavailable,
                detail: $"Service configuration for '{service}' was not found.",
                errorCode: "SERVICE_CONFIG_MISSING"
            );
        }

        var effectiveMode = _modeEvaluator.EvaluateMode(context, service);
        context.Items["GatewayMode"] = effectiveMode;
        using var activity = Activities.StartActivity($"{service}.{operation}");
        activity?.SetTag("backend.service", service);
        activity?.SetTag("backend.mode", effectiveMode);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));

        // 1. Phân loại MOCK
        if (effectiveMode == "Mock")
        {
            context.Response.Headers["X-Gateway-Mode"] = "Mock";
            context.Response.Headers.CacheControl = "no-store";

            var (dataElement, failure) = await _mockDataMapper.MapMockAsync(context, service, operation, config, parser, timeout.Token);
            if (failure != null) return failure;

            return _responseFactory.SuccessResult(context, dataElement, successMessage, successStatusCode);
        }

        // 2. Phân loại REAL (gRPC)
        context.Response.Headers["X-Gateway-Mode"] = "Real";

        if (_healthProberManager != null && !_healthProberManager.IsServiceAvailable(service))
        {
            return _responseFactory.FailureResult(
                context,
                title: "Downstream Service Unavailable",
                status: StatusCodes.Status503ServiceUnavailable,
                detail: $"Service '{service}' is currently unhealthy or undergoing recovery.",
                errorCode: "DOWNSTREAM_UNHEALTHY"
            );
        }

        var realData = await _grpcServiceCaller.CallAsync(context, realCall, config, timeout.Token);
        return _responseFactory.SuccessResult(context, realData, successMessage, successStatusCode);
    }

    public async Task<IResult> DispatchNoContentAsync<T>(
        HttpContext context,
        string service,
        string operation,
        Func<CallOptions, AsyncUnaryCall<T>> realCall,
        MessageParser<T> parser) where T : class, IMessage<T>
    {
        var config = _modeEvaluator.GetServiceOptions(service);
        if (config == null)
        {
            return _responseFactory.FailureResult(
                context,
                title: "Unknown backend service",
                status: StatusCodes.Status503ServiceUnavailable,
                detail: $"Service configuration for '{service}' was not found.",
                errorCode: "SERVICE_CONFIG_MISSING"
            );
        }

        var effectiveMode = _modeEvaluator.EvaluateMode(context, service);
        context.Items["GatewayMode"] = effectiveMode;
        using var activity = Activities.StartActivity($"{service}.{operation}");
        activity?.SetTag("backend.service", service);
        activity?.SetTag("backend.mode", effectiveMode);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));

        if (effectiveMode == "Mock")
        {
            context.Response.Headers["X-Gateway-Mode"] = "Mock";
            context.Response.Headers.CacheControl = "no-store";

            var (_, failure) = await _mockDataMapper.MapMockAsync(context, service, operation, config, parser, timeout.Token);
            if (failure != null) return failure;

            return Results.NoContent();
        }

        context.Response.Headers["X-Gateway-Mode"] = "Real";

        if (_healthProberManager != null && !_healthProberManager.IsServiceAvailable(service))
        {
            return _responseFactory.FailureResult(
                context,
                title: "Downstream Service Unavailable",
                status: StatusCodes.Status503ServiceUnavailable,
                detail: $"Service '{service}' is currently unhealthy or undergoing recovery.",
                errorCode: "DOWNSTREAM_UNHEALTHY"
            );
        }

        await _grpcServiceCaller.CallAsync(context, realCall, config, timeout.Token);
        return Results.NoContent();
    }

    public async Task<IResult> DispatchPagedAsync<T>(
        HttpContext context,
        string service,
        string operation,
        int pageIndex,
        int pageSize,
        Func<CallOptions, AsyncUnaryCall<T>> realCall,
        MessageParser<T> parser,
        int successStatusCode = StatusCodes.Status200OK) where T : class, IMessage<T>
    {
        var config = _modeEvaluator.GetServiceOptions(service);
        if (config == null)
        {
            return _responseFactory.FailureResult(
                context,
                title: "Unknown backend service",
                status: StatusCodes.Status503ServiceUnavailable,
                detail: $"Service configuration for '{service}' was not found.",
                errorCode: "SERVICE_CONFIG_MISSING"
            );
        }

        var effectiveMode = _modeEvaluator.EvaluateMode(context, service);
        context.Items["GatewayMode"] = effectiveMode;
        using var activity = Activities.StartActivity($"{service}.{operation}");
        activity?.SetTag("backend.service", service);
        activity?.SetTag("backend.mode", effectiveMode);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));

        string jsonResult;

        if (effectiveMode == "Mock")
        {
            context.Response.Headers["X-Gateway-Mode"] = "Mock";
            context.Response.Headers.CacheControl = "no-store";

            var (mockJson, failure) = await _mockDataMapper.MapMockPagedJsonAsync(context, service, operation, config, timeout.Token);
            if (failure != null) return failure;

            jsonResult = mockJson!;
        }
        else
        {
            context.Response.Headers["X-Gateway-Mode"] = "Real";

            if (_healthProberManager != null && !_healthProberManager.IsServiceAvailable(service))
            {
                return _responseFactory.FailureResult(
                    context,
                    title: "Downstream Service Unavailable",
                    status: StatusCodes.Status503ServiceUnavailable,
                    detail: $"Service '{service}' is currently unhealthy or undergoing recovery.",
                    errorCode: "DOWNSTREAM_UNHEALTHY"
                );
            }

            jsonResult = await _grpcServiceCaller.CallPagedJsonAsync(context, realCall, config, timeout.Token);
        }

        var protoMessage = parser.ParseJson(jsonResult);
        var canonicalJson = JsonFormatter.Default.Format(protoMessage);
        using var doc = JsonDocument.Parse(canonicalJson);

        var itemsList = new List<JsonElement>();
        var totalCount = 0;

        if (doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            if (doc.RootElement.TryGetProperty("items", out var itemsElem) && itemsElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in itemsElem.EnumerateArray())
                {
                    itemsList.Add(item.Clone());
                }
            }

            if (doc.RootElement.TryGetProperty("total", out var totalElem) && totalElem.TryGetInt32(out var t))
            {
                totalCount = t;
            }
            else if (doc.RootElement.TryGetProperty("totalCount", out var totalCountElem) && totalCountElem.TryGetInt32(out var tc))
            {
                totalCount = tc;
            }
            else
            {
                totalCount = itemsList.Count;
            }
        }

        return _responseFactory.PagedResult(context, itemsList, pageIndex, pageSize, totalCount, successStatusCode);
    }

    #endregion
}
