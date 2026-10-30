namespace Nexus.Gateway.Controllers;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.Gateway.Common;
using Nexus.Gateway.Dispatching;
using Nexus.Gateway.Dtos.Log;
using Nexus.Gateway.Responses;
using Nexus.Gateway.Security;

[ApiController]
[Route("api/v1/log")]
[BackendTag("log")]
[EnableRateLimiting("api")]
public sealed class LogController : GatewayBaseController
{
    private const string Service = "log";

    public LogController(IGatewayDispatcher dispatcher) : base(dispatcher)
    {
    }

    /// <summary>Queries system logs with pagination and filters.</summary>
    [HttpGet("logs")]
    [Authorize(GatewayPolicies.StaffOrAdmin)]
    [ProducesResponseType(typeof(ApiResponse<PagedData<object>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<IResult> QueryLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? from = null,
        [FromQuery] string? to = null,
        [FromQuery] string? serviceName = null,
        [FromQuery] string? eventType = null,
        [FromQuery] string? entityName = null,
        [FromQuery] string? entityId = null,
        [FromQuery] string? actorUserId = null,
        [FromQuery] string? correlationId = null,
        [FromQuery] string? logLevel = null)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
        {
            throw new GatewayInputException("Page must be at least 1 and pageSize between 1 and 100.", "Pagination");
        }

        var query = BuildQuery(
            ("from", from),
            ("to", to),
            ("serviceName", serviceName),
            ("eventType", eventType),
            ("entityName", entityName),
            ("entityId", entityId),
            ("actorUserId", actorUserId),
            ("correlationId", correlationId),
            ("logLevel", logLevel));

        return ForwardPagedAsync(
            Service,
            "/api/v1/log/logs",
            page,
            pageSize,
            mockSeed: "logs",
            queryParams: query);
    }

    /// <summary>Retrieves a single log entry by identifier.</summary>
    [HttpGet("logs/{id}")]
    [Authorize(GatewayPolicies.StaffOrAdmin)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IResult> GetLog([FromRoute] string id)
    {
        if (!Guid.TryParse(id, out var logId))
        {
            throw new GatewayInputException("Log ID must be a valid GUID.", "id");
        }

        return ForwardAsync(
            Service,
            $"/api/v1/log/logs/{logId}",
            HttpMethod.Get,
            mockSeed: "log",
            successMessage: "Log entry retrieved successfully.");
    }

    /// <summary>Lists registered log event types.</summary>
    [HttpGet("event-types")]
    [Authorize(GatewayPolicies.StaffOrAdmin)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<IResult> ListLogEventTypes()
    {
        return ForwardAsync(
            Service,
            "/api/v1/log/event-types",
            HttpMethod.Get,
            mockSeed: "event-types",
            successMessage: "Log event types retrieved successfully.");
    }

    /// <summary>Enables or disables a log event type.</summary>
    [HttpPut("event-types/{code}")]
    [Authorize(GatewayPolicies.Admin)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<IResult> SetLogEventTypeEnabled([FromRoute] string code, [FromBody] SetLogEventTypeEnabledDto body)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new GatewayInputException("Event type code is required.", "code");
        }

        return ForwardAsync(
            Service,
            $"/api/v1/log/event-types/{Uri.EscapeDataString(code.Trim())}",
            HttpMethod.Put,
            body: new { code = code.Trim(), isEnabled = body.IsEnabled },
            mockSeed: "event-type-update",
            successMessage: "Log event type updated successfully.");
    }

    /// <summary>Ingests a batch of log entries.</summary>
    [HttpPost("batch")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<IResult> BatchLog([FromBody] JsonElement payload)
    {
        if (payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            throw new GatewayInputException("Log batch payload is required.", "payload");
        }

        return ForwardAsync(
            Service,
            "/api/v1/log/batch",
            HttpMethod.Post,
            body: payload,
            mockSeed: "batch",
            successStatusCode: StatusCodes.Status202Accepted,
            successMessage: "Log batch accepted for processing.");
    }

    private static Dictionary<string, string?> BuildQuery(params (string Key, string? Value)[] filters)
    {
        var query = new Dictionary<string, string?>();
        foreach (var (key, value) in filters)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                query[key] = value;
            }
        }
        return query;
    }
}
