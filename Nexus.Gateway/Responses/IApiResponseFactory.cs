using Microsoft.AspNetCore.Http;

namespace Nexus.Gateway.Responses;

public interface IApiResponseFactory
{
    ApiResponse<T> CreateSuccess<T>(T data, string? message = null, int statusCode = 200, string? traceId = null);

    ApiResponse<PagedData<T>> CreatePaged<T>(IReadOnlyList<T> items, int pageIndex, int pageSize, int totalCount, int statusCode = 200, string? traceId = null);

    ApiErrorResponse CreateFailure(string title, int status, string? detail = null, string? errorCode = null, string? type = null, IDictionary<string, string[]>? errors = null, string? traceId = null);

    IResult SuccessResult<T>(HttpContext? context, T data, string? message = null, int statusCode = 200);

    IResult PagedResult<T>(HttpContext? context, IReadOnlyList<T> items, int pageIndex, int pageSize, int totalCount, int statusCode = 200);

    IResult FailureResult(HttpContext? context, string title, int status, string? detail = null, string? errorCode = null, string? type = null, IDictionary<string, string[]>? errors = null);
}
