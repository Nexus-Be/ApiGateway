namespace Nexus.Gateway.Dispatching;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Nexus.Gateway.Responses;

public interface IHttpBackendCaller
{
    Task<(JsonElement? Data, IResult? Failure)> CallAsync(
        HttpContext context,
        string service,
        RestForwardRequest request,
        BackendOptions config,
        CancellationToken cancellationToken);

    Task<(string? RawJson, IResult? Failure)> CallRawJsonAsync(
        HttpContext context,
        string service,
        string downstreamPath,
        HttpMethod method,
        object? body,
        IDictionary<string, string?>? queryParams,
        BackendOptions config,
        CancellationToken cancellationToken);
}

public sealed class HttpBackendCaller : IHttpBackendCaller
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IApiResponseFactory _responseFactory;

    public HttpBackendCaller(IHttpClientFactory httpClientFactory, IApiResponseFactory responseFactory)
    {
        _httpClientFactory = httpClientFactory;
        _responseFactory = responseFactory;
    }

    public async Task<(JsonElement? Data, IResult? Failure)> CallAsync(
        HttpContext context,
        string service,
        RestForwardRequest request,
        BackendOptions config,
        CancellationToken cancellationToken)
    {
        var (rawJson, failure) = await CallRawJsonAsync(
            context,
            service,
            request.DownstreamPath,
            request.Method,
            request.Body,
            request.QueryParams,
            config,
            cancellationToken);

        if (failure != null) return (null, failure);
        if (string.IsNullOrWhiteSpace(rawJson)) return (null, null);

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("success", out var successProp) &&
                successProp.ValueKind == JsonValueKind.True &&
                root.TryGetProperty("data", out var dataProp))
            {
                return (dataProp.Clone(), null);
            }

            return (root.Clone(), null);
        }
        catch (JsonException ex)
        {
            return (null, _responseFactory.FailureResult(
                context,
                title: "Invalid JSON from downstream service",
                status: StatusCodes.Status502BadGateway,
                detail: ex.Message,
                errorCode: "BAD_GATEWAY"
            ));
        }
    }

    public async Task<(string? RawJson, IResult? Failure)> CallRawJsonAsync(
        HttpContext context,
        string service,
        string downstreamPath,
        HttpMethod method,
        object? body,
        IDictionary<string, string?>? queryParams,
        BackendOptions config,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(service);
        var baseAddress = client.BaseAddress?.ToString() ?? config.Address;

        if (string.IsNullOrWhiteSpace(baseAddress))
        {
            return (null, _responseFactory.FailureResult(
                context,
                title: "Downstream service address not configured",
                status: StatusCodes.Status503ServiceUnavailable,
                detail: $"Address for service '{service}' is missing.",
                errorCode: "SERVICE_ADDRESS_MISSING"
            ));
        }

        var baseUrl = baseAddress.TrimEnd('/');
        var path = downstreamPath.StartsWith('/') ? downstreamPath : "/" + downstreamPath;
        var uriBuilder = new UriBuilder(baseUrl + path);

        if (queryParams != null && queryParams.Count > 0)
        {
            var queryList = new List<string>();
            foreach (var kvp in queryParams)
            {
                if (!string.IsNullOrEmpty(kvp.Value))
                {
                    queryList.Add($"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}");
                }
            }
            if (queryList.Count > 0)
            {
                uriBuilder.Query = string.Join("&", queryList);
            }
        }

        using var requestMessage = new HttpRequestMessage(method, uriBuilder.Uri);

        if (body != null)
        {
            if (body is HttpContent httpContent)
            {
                requestMessage.Content = httpContent;
            }
            else
            {
                var json = JsonSerializer.Serialize(body, JsonOptions);
                requestMessage.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }
        }

        ForwardContextHeaders(context, requestMessage);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseContentRead, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            return (null, _responseFactory.FailureResult(
                context,
                title: "Downstream service unavailable",
                status: StatusCodes.Status503ServiceUnavailable,
                detail: ex.Message,
                errorCode: "SERVICE_UNAVAILABLE"
            ));
        }
        catch (TaskCanceledException) when (!context.RequestAborted.IsCancellationRequested)
        {
            return (null, _responseFactory.FailureResult(
                context,
                title: "Downstream request timed out",
                status: StatusCodes.Status504GatewayTimeout,
                detail: $"Service '{service}' timed out after {config.TimeoutSeconds}s.",
                errorCode: "GATEWAY_TIMEOUT"
            ));
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string title = "Downstream service error";
            string? detail = string.IsNullOrWhiteSpace(content) ? null : content;
            string? errorCode = null;
            IDictionary<string, string[]>? errors = null;

            if (!string.IsNullOrWhiteSpace(content))
            {
                try
                {
                    using var errorDoc = JsonDocument.Parse(content);
                    var errRoot = errorDoc.RootElement;
                    if (errRoot.ValueKind == JsonValueKind.Object)
                    {
                        if (errRoot.TryGetProperty("title", out var titleProp)) title = titleProp.GetString() ?? title;
                        if (errRoot.TryGetProperty("message", out var msgProp)) title = msgProp.GetString() ?? title;
                        if (errRoot.TryGetProperty("detail", out var detailProp)) detail = detailProp.GetString();
                        if (errRoot.TryGetProperty("errorCode", out var codeProp)) errorCode = codeProp.GetString();
                        if (errRoot.TryGetProperty("errors", out var errorsProp) && errorsProp.ValueKind == JsonValueKind.Object)
                        {
                            errors = new Dictionary<string, string[]>();
                            foreach (var prop in errorsProp.EnumerateObject())
                            {
                                if (prop.Value.ValueKind == JsonValueKind.Array)
                                {
                                    var errList = new List<string>();
                                    foreach (var item in prop.Value.EnumerateArray()) errList.Add(item.GetString() ?? "");
                                    errors[prop.Name] = errList.ToArray();
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Fallback to raw detail
                }
            }

            return (null, _responseFactory.FailureResult(
                context,
                title: title,
                status: (int)response.StatusCode,
                detail: detail,
                errorCode: errorCode ?? "DOWNSTREAM_ERROR",
                errors: errors
            ));
        }

        return (content, null);
    }

    private static void ForwardContextHeaders(HttpContext context, HttpRequestMessage request)
    {
        if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            request.Headers.TryAddWithoutValidation("Authorization", (string?)authHeader);
        }

        if (context.Request.Headers.TryGetValue("x-trace-id", out var traceHeader) && !string.IsNullOrWhiteSpace(traceHeader))
        {
            request.Headers.TryAddWithoutValidation("x-trace-id", (string?)traceHeader);
        }
        else
        {
            var traceId = ApiResponseFactory.ResolveTraceId(context);
            request.Headers.TryAddWithoutValidation("x-trace-id", traceId);
        }

        var user = context.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user.FindFirst("sub")?.Value
                ?? user.FindFirst("id")?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                request.Headers.TryAddWithoutValidation("x-user-id", userId);
            }

            var roles = user.FindAll(ClaimTypes.Role);
            var roleNames = new List<string>();
            foreach (var r in roles) roleNames.Add(r.Value);
            if (roleNames.Count > 0)
            {
                request.Headers.TryAddWithoutValidation("x-user-roles", string.Join(",", roleNames));
            }

            var email = user.FindFirst(ClaimTypes.Email)?.Value;
            if (!string.IsNullOrEmpty(email))
            {
                request.Headers.TryAddWithoutValidation("x-user-email", email);
            }
        }
        else
        {
            if (context.Request.Headers.TryGetValue("x-user-id", out var uid))
                request.Headers.TryAddWithoutValidation("x-user-id", (string?)uid);
            if (context.Request.Headers.TryGetValue("x-user-roles", out var uroles))
                request.Headers.TryAddWithoutValidation("x-user-roles", (string?)uroles);
            if (context.Request.Headers.TryGetValue("x-user-email", out var umail))
                request.Headers.TryAddWithoutValidation("x-user-email", (string?)umail);
        }

        if (context.Request.Headers.TryGetValue("Idempotency-Key", out var idemp))
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", (string?)idemp);
        }
    }
}
