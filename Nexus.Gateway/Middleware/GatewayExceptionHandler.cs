using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Nexus.Gateway.Common;
using Nexus.Gateway.Responses;

namespace Nexus.Gateway.Middleware;

public sealed class GatewayExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GatewayExceptionHandler> _logger;
    private readonly IApiResponseFactory _responseFactory;

    public GatewayExceptionHandler(ILogger<GatewayExceptionHandler> logger, IApiResponseFactory responseFactory)
    {
        _logger = logger;
        _responseFactory = responseFactory;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, errorCode, detail) = MapException(context, exception);

        if (exception is GatewayInputException)
            _logger.LogInformation("Request validation failed ({ErrorCode}). Trace: {TraceId}", errorCode, context.TraceIdentifier);
        else
            _logger.LogWarning(exception, "Request failed with status {Status} ({ErrorCode}): {Message}. Trace: {TraceId}",
                status, errorCode, exception.Message, context.TraceIdentifier);

        IDictionary<string, string[]>? errors = exception is GatewayInputException input
            ? new Dictionary<string, string[]> { [input.Field] = [input.Message] }
            : null;
        var errorResponse = _responseFactory.CreateFailure(
            title: title,
            status: status,
            detail: detail,
            errorCode: errorCode,
            errors: errors,
            traceId: ApiResponseFactory.ResolveTraceId(context)
        );

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(errorResponse, cancellationToken);
        return true;
    }

    private static (int Status, string Title, string ErrorCode, string? Detail) MapException(HttpContext context, Exception exception)
    {
        return exception switch
        {
            GatewayInputException input =>
                (400, "One or more validation errors occurred.", input.ErrorCode, input.Message),

            HttpRequestException httpEx => (
                httpEx.StatusCode.HasValue ? (int)httpEx.StatusCode.Value : 502,
                "Downstream service communication failure",
                "BAD_GATEWAY",
                httpEx.Message),

            OperationCanceledException when !context.RequestAborted.IsCancellationRequested =>
                (504, "Gateway timeout", "GATEWAY_TIMEOUT", "The request timed out before the upstream service could respond."),

            BadHttpRequestException bad =>
                (bad.StatusCode, "Bad HTTP request", "BAD_REQUEST", bad.Message),

            _ => (500, "Internal server error", "INTERNAL_ERROR", "An internal gateway error occurred.")
        };
    }
}
