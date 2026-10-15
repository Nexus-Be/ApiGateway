using Grpc.Core;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
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

        _logger.LogWarning(exception, "Request failed with status {Status} ({ErrorCode}): {Message}. Trace: {TraceId}",
            status, errorCode, exception.Message, context.TraceIdentifier);

        var errorResponse = _responseFactory.CreateFailure(
            title: title,
            status: status,
            detail: detail,
            errorCode: errorCode,
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
            RpcException { StatusCode: StatusCode.Cancelled } when !context.RequestAborted.IsCancellationRequested =>
                (504, "Gateway timeout", "GATEWAY_TIMEOUT", "Upstream operation timed out or was cancelled."),

            RpcException rpc => rpc.StatusCode switch
            {
                StatusCode.InvalidArgument => (400, "One or more validation errors occurred.", "VAL_INVALID_INPUT", "The input arguments were invalid."),
                StatusCode.Unauthenticated => (401, "Authentication required", "AUTH_UNAUTHENTICATED", "A valid authentication token is required."),
                StatusCode.PermissionDenied => (403, "Access denied", "AUTH_FORBIDDEN", "You do not have permission to perform this action."),
                StatusCode.NotFound => (404, "Resource not found", "NOT_FOUND", "The requested resource was not found."),
                StatusCode.AlreadyExists or StatusCode.Aborted => (409, "Resource conflict", "CONFLICT", "The resource already exists or conflict occurred."),
                StatusCode.FailedPrecondition => (422, "Precondition failed", "FAILED_PRECONDITION", "The operation could not be processed due to precondition failure."),
                StatusCode.ResourceExhausted => (429, "Rate limit exceeded", "RATE_LIMIT_EXCEEDED", "Resource limit or rate limit exceeded."),
                StatusCode.Unimplemented => (501, "Operation not implemented", "NOT_IMPLEMENTED", "The requested operation is not implemented by the upstream service."),
                StatusCode.Unavailable => (503, "Backend service unavailable", "SERVICE_UNAVAILABLE", "Upstream service is currently unavailable."),
                StatusCode.DeadlineExceeded => (504, "Gateway timeout", "GATEWAY_TIMEOUT", "Upstream service call timed out."),
                _ => (502, "Bad gateway", "BAD_GATEWAY", "Upstream service returned an unexpected status.")
            },

            OperationCanceledException when !context.RequestAborted.IsCancellationRequested =>
                (504, "Gateway timeout", "GATEWAY_TIMEOUT", "The request timed out before the upstream service could respond."),

            BadHttpRequestException bad =>
                (bad.StatusCode, "Bad HTTP request", "BAD_REQUEST", bad.Message),

            _ => (500, "Internal server error", "INTERNAL_ERROR", "An internal gateway error occurred.")
        };
    }
}
