using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Nexus.Gateway.Responses;

public sealed class ApiResponseFactory : IApiResponseFactory
{
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public ApiResponseFactory(IHttpContextAccessor? httpContextAccessor = null)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public static string ResolveTraceId(HttpContext? context)
    {
        // The request ID must remain stable even when dispatching creates a child Activity.
        // Otherwise the response header, response body and outbound gRPC metadata disagree.
        return context?.TraceIdentifier
            ?? Activity.Current?.Id
            ?? Guid.NewGuid().ToString("N");
    }

    public static string GetCurrentTimestamp() =>
        DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

    public ApiResponse<T> CreateSuccess<T>(T data, string? message = null, int statusCode = 200, string? traceId = null)
    {
        var resolvedTraceId = traceId ?? ResolveTraceId(_httpContextAccessor?.HttpContext);
        return new ApiResponse<T>
        {
            Success = true,
            StatusCode = statusCode,
            Message = message,
            Data = data,
            TraceId = resolvedTraceId,
            Timestamp = GetCurrentTimestamp()
        };
    }

    public ApiResponse<PagedData<T>> CreatePaged<T>(IReadOnlyList<T> items, int pageIndex, int pageSize, int totalCount, int statusCode = 200, string? traceId = null)
    {
        var resolvedTraceId = traceId ?? ResolveTraceId(_httpContextAccessor?.HttpContext);
        var pagedData = new PagedData<T>
        {
            Items = items,
            Pagination = PaginationMetadata.Create(pageIndex, pageSize, totalCount)
        };

        return new ApiResponse<PagedData<T>>
        {
            Success = true,
            StatusCode = statusCode,
            Data = pagedData,
            TraceId = resolvedTraceId,
            Timestamp = GetCurrentTimestamp()
        };
    }

    public static string ResolveStandardType(int status) => status switch
    {
        400 => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
        401 => "https://tools.ietf.org/html/rfc9110#section-15.5.2",
        403 => "https://tools.ietf.org/html/rfc9110#section-15.5.4",
        404 => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
        405 => "https://tools.ietf.org/html/rfc9110#section-15.5.6",
        408 => "https://tools.ietf.org/html/rfc9110#section-15.5.9",
        409 => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
        415 => "https://tools.ietf.org/html/rfc9110#section-15.5.16",
        422 => "https://tools.ietf.org/html/rfc9110#section-15.5.21",
        429 => "https://tools.ietf.org/html/rfc6585#section-4",
        500 => "https://tools.ietf.org/html/rfc9110#section-15.6.1",
        501 => "https://tools.ietf.org/html/rfc9110#section-15.6.2",
        502 => "https://tools.ietf.org/html/rfc9110#section-15.6.3",
        503 => "https://tools.ietf.org/html/rfc9110#section-15.6.4",
        504 => "https://tools.ietf.org/html/rfc9110#section-15.6.5",
        _ => "https://tools.ietf.org/html/rfc9110"
    };

    public ApiErrorResponse CreateFailure(string title, int status, string? detail = null, string? errorCode = null, string? type = null, IDictionary<string, string[]>? errors = null, string? traceId = null)
    {
        var resolvedTraceId = traceId ?? ResolveTraceId(_httpContextAccessor?.HttpContext);
        var resolvedType = type ?? ResolveStandardType(status);

        return new ApiErrorResponse
        {
            Type = resolvedType,
            Title = title,
            Status = status,
            ErrorCode = errorCode,
            Detail = detail,
            Errors = errors,
            TraceId = resolvedTraceId,
            Timestamp = GetCurrentTimestamp()
        };
    }

    public IResult SuccessResult<T>(HttpContext? context, T data, string? message = null, int statusCode = 200)
    {
        var traceId = ResolveTraceId(context ?? _httpContextAccessor?.HttpContext);
        var response = CreateSuccess(data, message, statusCode, traceId);
        return Results.Json(response, statusCode: statusCode);
    }

    public IResult PagedResult<T>(HttpContext? context, IReadOnlyList<T> items, int pageIndex, int pageSize, int totalCount, int statusCode = 200)
    {
        var traceId = ResolveTraceId(context ?? _httpContextAccessor?.HttpContext);
        var response = CreatePaged(items, pageIndex, pageSize, totalCount, statusCode, traceId);
        return Results.Json(response, statusCode: statusCode);
    }

    public IResult FailureResult(HttpContext? context, string title, int status, string? detail = null, string? errorCode = null, string? type = null, IDictionary<string, string[]>? errors = null)
    {
        var traceId = ResolveTraceId(context ?? _httpContextAccessor?.HttpContext);
        var response = CreateFailure(title, status, detail, errorCode, type, errors, traceId);
        return Results.Json(response, statusCode: status);
    }

    // Static convenience helpers
    public static ApiResponse<T> Success<T>(
        T data,
        string? message = null,
        string? traceId = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            StatusCode = 200,
            Message = message,
            Data = data,
            Errors = null,
            TraceId = traceId ?? ResolveTraceId(null),
            Timestamp = GetCurrentTimestamp()
        };
    }

    public static ApiResponse<T> Error<T>(
        string message,
        IEnumerable<ApiError>? errors = null,
        string? traceId = null,
        int statusCode = 400)
    {
        return new ApiResponse<T>
        {
            Success = false,
            StatusCode = statusCode,
            Message = message,
            Data = default,
            Errors = errors?.ToArray(),
            TraceId = traceId ?? ResolveTraceId(null),
            Timestamp = GetCurrentTimestamp()
        };
    }

    public static IResult Success<T>(HttpContext context, T data, string? message = null, int statusCode = 200)
    {
        var traceId = ResolveTraceId(context);
        var resp = new ApiResponse<T>
        {
            Success = true,
            StatusCode = statusCode,
            Message = message,
            Data = data,
            TraceId = traceId,
            Timestamp = GetCurrentTimestamp()
        };
        return Results.Json(resp, statusCode: statusCode);
    }

    public static IResult Paged<T>(HttpContext context, IReadOnlyList<T> items, int pageIndex, int pageSize, int totalCount, int statusCode = 200)
    {
        var traceId = ResolveTraceId(context);
        var resp = new ApiResponse<PagedData<T>>
        {
            Success = true,
            StatusCode = statusCode,
            Data = new PagedData<T>
            {
                Items = items,
                Pagination = PaginationMetadata.Create(pageIndex, pageSize, totalCount)
            },
            TraceId = traceId,
            Timestamp = GetCurrentTimestamp()
        };
        return Results.Json(resp, statusCode: statusCode);
    }

    public static IResult Failure(HttpContext context, string title, int status, string? detail = null, string? errorCode = null, string? type = null, IDictionary<string, string[]>? errors = null)
    {
        var traceId = ResolveTraceId(context);
        var resolvedType = type ?? ResolveStandardType(status);

        var resp = new ApiErrorResponse
        {
            Type = resolvedType,
            Title = title,
            Status = status,
            ErrorCode = errorCode,
            Detail = detail,
            Errors = errors,
            TraceId = traceId,
            Timestamp = GetCurrentTimestamp()
        };
        return Results.Json(resp, statusCode: status);
    }

    public static bool PageOk(int page, int size) => page > 0 && size is >= 1 and <= 100;
    public static bool GuidOk(string? value) => string.IsNullOrEmpty(value) || Guid.TryParse(value, out _);

    public static IResult BadRequest(HttpContext context, string message, string field = "General", string errorCode = "VAL_INVALID_INPUT")
    {
        var errors = new Dictionary<string, string[]>
        {
            [field] = [message]
        };
        return Failure(
            context,
            title: "One or more validation errors occurred.",
            status: StatusCodes.Status400BadRequest,
            detail: message,
            errorCode: errorCode,
            type: "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            errors: errors
        );
    }
}
