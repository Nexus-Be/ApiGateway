using System.Diagnostics;
using System.Text.Json;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexus.Gateway.Responses;
using Nexus.Gateway.Services;

namespace Nexus.Gateway;

public sealed class BackendDispatcher
{
    public const string ActivityName = "Nexus.Gateway.Backend";
    private static readonly ActivitySource Activities = new(ActivityName);

    private readonly IOptionsMonitor<GatewayOptions> _options;
    private readonly IGatewayRuntimeManager _runtimeManager;
    private readonly IWebHostEnvironment _environment;
    private readonly IApiResponseFactory _responseFactory;
    private readonly ILogger<BackendDispatcher> _logger;

    public BackendDispatcher(
        IOptionsMonitor<GatewayOptions> options,
        IGatewayRuntimeManager runtimeManager,
        IWebHostEnvironment environment,
        IApiResponseFactory responseFactory,
        ILogger<BackendDispatcher> logger)
    {
        _options = options;
        _runtimeManager = runtimeManager;
        _environment = environment;
        _responseFactory = responseFactory;
        _logger = logger;
    }

    public async Task<IResult> Execute<T>(
        HttpContext context,
        string service,
        string operation,
        Func<CallOptions, AsyncUnaryCall<T>> call,
        MessageParser<T> parser,
        string? successMessage = null,
        int successStatusCode = StatusCodes.Status200OK) where T : class, IMessage<T>
    {
        var config = _options.CurrentValue.GetService(service);
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

        var effectiveMode = _runtimeManager.ResolveEffectiveMode(context, service, config.Mode);
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

            if (config.MockDelayMs > 0)
            {
                await Task.Delay(config.MockDelayMs, timeout.Token);
            }

            if (config.MockStatus != StatusCodes.Status200OK)
            {
                return _responseFactory.FailureResult(
                    context,
                    title: "Configured mock failure",
                    status: config.MockStatus,
                    detail: $"Mock returned configured failure status {config.MockStatus}.",
                    errorCode: "MOCK_FAILURE"
                );
            }

            var fixturePath = Path.Combine(_environment.ContentRootPath, "Mocks", service, $"{operation}.json");
            if (!File.Exists(fixturePath))
            {
                // Fallback for user/users
                if (service.Equals("user", StringComparison.OrdinalIgnoreCase))
                    fixturePath = Path.Combine(_environment.ContentRootPath, "Mocks", "users", $"{operation}.json");
                else if (service.Equals("users", StringComparison.OrdinalIgnoreCase))
                    fixturePath = Path.Combine(_environment.ContentRootPath, "Mocks", "user", $"{operation}.json");
            }

            if (!File.Exists(fixturePath))
            {
                return _responseFactory.FailureResult(
                    context,
                    title: "Mock fixture not found",
                    status: StatusCodes.Status500InternalServerError,
                    detail: $"Mock fixture file '{fixturePath}' is missing.",
                    errorCode: "MOCK_FIXTURE_NOT_FOUND"
                );
            }

            var json = await File.ReadAllTextAsync(fixturePath, timeout.Token);
            var protoMessage = parser.ParseJson(json);
            var formattedJson = JsonFormatter.Default.Format(protoMessage);
            using var doc = JsonDocument.Parse(formattedJson);
            var dataElement = doc.RootElement.Clone();

            return _responseFactory.SuccessResult(context, dataElement, successMessage, successStatusCode);
        }

        // Real gRPC Call
        context.Response.Headers["X-Gateway-Mode"] = "Real";
        var headers = new Metadata();

        var authorization = context.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(authorization))
        {
            headers.Add("authorization", authorization);
        }

        if (context.Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKey))
        {
            headers.Add("idempotency-key", idempotencyKey.ToString());
        }

        var traceId = ApiResponseFactory.ResolveTraceId(context);
        headers.Add("x-trace-id", traceId);

        var callOptions = new CallOptions(
            headers,
            deadline: DateTime.UtcNow.AddSeconds(config.TimeoutSeconds),
            cancellationToken: timeout.Token
        );

        using var unaryCall = call(callOptions);
        var response = await unaryCall.ResponseAsync;
        var realFormattedJson = JsonFormatter.Default.Format(response);
        using var realDoc = JsonDocument.Parse(realFormattedJson);
        var realDataElement = realDoc.RootElement.Clone();

        return _responseFactory.SuccessResult(context, realDataElement, successMessage, successStatusCode);
    }

    public async Task<IResult> ExecutePaged<T>(
        HttpContext context,
        string service,
        string operation,
        int pageIndex,
        int pageSize,
        Func<CallOptions, AsyncUnaryCall<T>> call,
        MessageParser<T> parser,
        int successStatusCode = StatusCodes.Status200OK) where T : class, IMessage<T>
    {
        var config = _options.CurrentValue.GetService(service);
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

        var effectiveMode = _runtimeManager.ResolveEffectiveMode(context, service, config.Mode);
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

            if (config.MockDelayMs > 0)
            {
                await Task.Delay(config.MockDelayMs, timeout.Token);
            }

            if (config.MockStatus != StatusCodes.Status200OK)
            {
                return _responseFactory.FailureResult(
                    context,
                    title: "Configured mock failure",
                    status: config.MockStatus,
                    detail: $"Mock returned configured failure status {config.MockStatus}.",
                    errorCode: "MOCK_FAILURE"
                );
            }

            var fixturePath = Path.Combine(_environment.ContentRootPath, "Mocks", service, $"{operation}.json");
            if (!File.Exists(fixturePath))
            {
                // Fallback for user/users
                if (service.Equals("user", StringComparison.OrdinalIgnoreCase))
                    fixturePath = Path.Combine(_environment.ContentRootPath, "Mocks", "users", $"{operation}.json");
                else if (service.Equals("users", StringComparison.OrdinalIgnoreCase))
                    fixturePath = Path.Combine(_environment.ContentRootPath, "Mocks", "user", $"{operation}.json");
            }

            if (!File.Exists(fixturePath))
            {
                return _responseFactory.FailureResult(
                    context,
                    title: "Mock fixture not found",
                    status: StatusCodes.Status500InternalServerError,
                    detail: $"Mock fixture file '{fixturePath}' is missing.",
                    errorCode: "MOCK_FIXTURE_NOT_FOUND"
                );
            }

            var fileContent = await File.ReadAllTextAsync(fixturePath, timeout.Token);
            var protoMessage = parser.ParseJson(fileContent);
            jsonResult = JsonFormatter.Default.Format(protoMessage);
        }
        else
        {
            // Real gRPC Call
            context.Response.Headers["X-Gateway-Mode"] = "Real";
            var headers = new Metadata();

            var authorization = context.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrEmpty(authorization))
            {
                headers.Add("authorization", authorization);
            }

            var traceId = ApiResponseFactory.ResolveTraceId(context);
            headers.Add("x-trace-id", traceId);

            var callOptions = new CallOptions(
                headers,
                deadline: DateTime.UtcNow.AddSeconds(config.TimeoutSeconds),
                cancellationToken: timeout.Token
            );

            using var unaryCall = call(callOptions);
            var response = await unaryCall.ResponseAsync;
            jsonResult = JsonFormatter.Default.Format(response);
        }

        using var doc = JsonDocument.Parse(jsonResult);
        var root = doc.RootElement;

        var itemsList = new List<JsonElement>();
        if (root.TryGetProperty("items", out var itemsElement) && itemsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in itemsElement.EnumerateArray())
            {
                itemsList.Add(item.Clone());
            }
        }

        var total = 0;
        if (root.TryGetProperty("total", out var totalElement) && totalElement.TryGetInt32(out var parsedTotal))
        {
            total = parsedTotal;
        }

        return _responseFactory.PagedResult(context, itemsList, pageIndex, pageSize, total, successStatusCode);
    }
}
