namespace Nexus.Gateway.Controllers;

using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
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
[ApiController]
[Route("api/v1")]
[BackendTag("log")]
[EnableRateLimiting("api")]
public sealed class LogController : GatewayBaseController
{
    private const string Service = "log";

    public LogController(IGatewayDispatcher dispatcher) : base(dispatcher)
    {
    }

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

        var query = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(from)) query["from"] = from;
        if (!string.IsNullOrWhiteSpace(to)) query["to"] = to;
        if (!string.IsNullOrWhiteSpace(serviceName)) query["serviceName"] = serviceName;
        if (!string.IsNullOrWhiteSpace(eventType)) query["eventType"] = eventType;
        if (!string.IsNullOrWhiteSpace(entityName)) query["entityName"] = entityName;
        if (!string.IsNullOrWhiteSpace(entityId)) query["entityId"] = entityId;
        if (!string.IsNullOrWhiteSpace(actorUserId)) query["actorUserId"] = actorUserId;
        if (!string.IsNullOrWhiteSpace(correlationId)) query["correlationId"] = correlationId;
        if (!string.IsNullOrWhiteSpace(logLevel)) query["logLevel"] = logLevel;

        return ForwardPagedAsync(
            Service,
            "/api/v1/log/logs",
            pagination.Page,
            pagination.PageSize,
            HttpMethod.Get,
            mockSeed: "logs",
            queryParams: query
        );
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

        var query = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(from)) query["from"] = from;
        if (!string.IsNullOrWhiteSpace(to)) query["to"] = to;
        if (!string.IsNullOrWhiteSpace(serviceName)) query["serviceName"] = serviceName;
        if (!string.IsNullOrWhiteSpace(eventType)) query["eventType"] = eventType;
        if (!string.IsNullOrWhiteSpace(entityName)) query["entityName"] = entityName;
        if (!string.IsNullOrWhiteSpace(entityId)) query["entityId"] = entityId;
        if (!string.IsNullOrWhiteSpace(actorUserId)) query["actorUserId"] = actorUserId;
        if (!string.IsNullOrWhiteSpace(correlationId)) query["correlationId"] = correlationId;
        if (!string.IsNullOrWhiteSpace(logLevel)) query["logLevel"] = logLevel;

        return ForwardPagedAsync(
            Service,
            "/api/v1/admin/logs",
            pagination.Page,
            pagination.PageSize,
            HttpMethod.Get,
            mockSeed: "logs",
            queryParams: query
        );
    }

    /// <summary>Xem chi tiết một bản ghi nhật ký (LogApi.GetLog)</summary>
    [HttpGet("log/logs/{id}")]
    [Authorize(Policy = GatewayPolicies.Staff)]
    public Task<IResult> GetLog(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new GatewayInputException("Log ID is required.", "Id");

        return ForwardAsync(
            Service,
            $"/api/v1/log/logs/{id}",
            HttpMethod.Get,
            mockSeed: "log"
        );
    }

    /// <summary>Danh mục các loại sự kiện nhật ký hệ thống (LogApi.ListLogEventTypes)</summary>
    [HttpGet("log/event-types")]
    [Authorize(Policy = GatewayPolicies.Staff)]
    public Task<IResult> ListLogEventTypes()
    {
        return ForwardAsync(
            Service,
            "/api/v1/log/event-types",
            HttpMethod.Get,
            mockSeed: "event-types"
        );
    }

    /// <summary>Bật hoặc tắt ghi nhật ký cho một loại sự kiện (LogApi.SetLogEventTypeEnabled)</summary>
    [HttpPut("log/event-types/{code}")]
    [Authorize(Policy = GatewayPolicies.Admin)]
    public Task<IResult> SetLogEventTypeEnabled(string code, [FromBody] SetLogEventTypeEnabledDto dto)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new GatewayInputException("Event type code is required.", "Code");

        return ForwardAsync(
            Service,
            $"/api/v1/log/event-types/{code}",
            HttpMethod.Put,
            body: dto,
            mockSeed: "event-type-update",
            successMessage: "Log event type status updated successfully."
        );
    }

    /// <summary>Gửi batch log bất đồng bộ từ các client/hạ tầng</summary>
    [HttpPost("log/batch")]
    [Authorize(Policy = GatewayPolicies.Staff)]
    public Task<IResult> BatchLog([FromBody] BatchLogRequestDto request)
    {
        return ForwardAsync(
            Service,
            "/api/v1/log/batch",
            HttpMethod.Post,
            body: request,
            mockSeed: "batch",
            successMessage: "Log batch accepted.",
            successStatusCode: StatusCodes.Status202Accepted
        );
    }
}
