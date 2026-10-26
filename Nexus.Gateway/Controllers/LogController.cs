namespace Nexus.Gateway.Controllers;

using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.Gateway.Common;
using Nexus.Gateway.Dispatching;
using Nexus.Gateway.Dtos.Log;
using Nexus.Gateway.Security;

/// <summary>
/// Log surface (specs/02_GATEWAY_CONTROLLERS_ROUTING.md §14).
/// Provides audit and system log querying, log event type management, and batch ingestion.
/// </summary>
/// <remarks>
/// <para>Mock mode reads <c>Seeds/log/*.json</c> via <c>IGatewayDispatcher</c>.</para>
/// <para>Real mode connects to Log.Api gRPC server (port 8004) once enabled.</para>
/// </remarks>
[ApiController]
[Route("api/v1")]
[BackendTag("log")]
[EnableRateLimiting("api")]
public sealed class LogController : ControllerBase
{
    private const string Service = "log";

    // Mock-only pass-through: in Mock mode GatewayDispatcher never invokes realCall, only the
    // parser. Struct round-trips the seed JSON object, so no log.proto is needed for Gateway Mock.
    private static readonly MessageParser<Struct> SeedParser = Struct.Parser;

    private readonly IGatewayDispatcher _dispatcher;

    public LogController(IGatewayDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    private static AsyncUnaryCall<Struct> PendingRealCall(CallOptions _) =>
        throw new NotSupportedException("The log gRPC contract is not wired yet in Gateway client.");

    /// <summary>Truy vấn nhật ký hệ thống có phân trang và bộ lọc (LogApi.QueryLogs)</summary>
    [HttpGet("log/logs")]
    [Authorize(Policy = GatewayPolicies.Staff)]
    public Task<IResult> QueryLogs(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? serviceName,
        [FromQuery] string? eventType,
        [FromQuery] string? entityName,
        [FromQuery] string? entityId,
        [FromQuery] string? actorUserId,
        [FromQuery] string? correlationId,
        [FromQuery] string? logLevel)
    {
        pagination.EnsureValid();

        return _dispatcher.DispatchPagedAsync(
            HttpContext,
            Service,
            "logs",
            pagination.Page,
            pagination.PageSize,
            PendingRealCall,
            SeedParser);
    }

    /// <summary>Alias truy vấn nhật ký dành cho Admin/Staff</summary>
    [HttpGet("admin/logs")]
    [Authorize(Policy = GatewayPolicies.Staff)]
    public Task<IResult> AdminQueryLogs(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? serviceName,
        [FromQuery] string? eventType,
        [FromQuery] string? entityName,
        [FromQuery] string? entityId,
        [FromQuery] string? actorUserId,
        [FromQuery] string? correlationId,
        [FromQuery] string? logLevel)
    {
        pagination.EnsureValid();

        return _dispatcher.DispatchPagedAsync(
            HttpContext,
            Service,
            "logs",
            pagination.Page,
            pagination.PageSize,
            PendingRealCall,
            SeedParser);
    }

    /// <summary>Xem chi tiết một bản ghi nhật ký (LogApi.GetLog)</summary>
    [HttpGet("log/logs/{id}")]
    [Authorize(Policy = GatewayPolicies.Staff)]
    public Task<IResult> GetLog(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new GatewayInputException("Log ID is required.", "Id");

        return _dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "log",
            PendingRealCall,
            SeedParser);
    }

    /// <summary>Danh mục các loại sự kiện nhật ký hệ thống (LogApi.ListLogEventTypes)</summary>
    [HttpGet("log/event-types")]
    [Authorize(Policy = GatewayPolicies.Staff)]
    public Task<IResult> ListLogEventTypes()
    {
        return _dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "event-types",
            PendingRealCall,
            SeedParser);
    }

    /// <summary>Bật hoặc tắt ghi nhật ký cho một loại sự kiện (LogApi.SetLogEventTypeEnabled)</summary>
    [HttpPut("log/event-types/{code}")]
    [Authorize(Policy = GatewayPolicies.Admin)]
    public Task<IResult> SetLogEventTypeEnabled(string code, [FromBody] SetLogEventTypeEnabledDto dto)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new GatewayInputException("Event type code is required.", "Code");

        return _dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "event-type-update",
            PendingRealCall,
            SeedParser,
            successMessage: "Log event type status updated successfully.");
    }

    /// <summary>Gửi batch log bất đồng bộ từ các client/hạ tầng</summary>
    [HttpPost("log/batch")]
    [Authorize(Policy = GatewayPolicies.Staff)]
    public Task<IResult> BatchLog([FromBody] BatchLogRequestDto request)
    {
        return _dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "batch",
            PendingRealCall,
            SeedParser,
            successMessage: "Log batch accepted.",
            successStatusCode: StatusCodes.Status202Accepted);
    }
}
