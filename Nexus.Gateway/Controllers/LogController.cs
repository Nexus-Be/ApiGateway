namespace Nexus.Gateway.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.Gateway.Common;
using Nexus.Gateway.Dispatching;
using Nexus.Gateway.Dtos.Log;
using Nexus.Gateway.Responses;
using Nexus.Gateway.Security;
using ShoppingCart.Contracts.V1;

[ApiController]
[Route("api/v1/log")]
[Route("api/v1/audit")]
[BackendTag("log")]
[EnableRateLimiting("api")]
public sealed class LogController : ControllerBase
{
    private readonly IGatewayDispatcher _dispatcher;
    private readonly LogApi.LogApiClient _client;

    public LogController(IGatewayDispatcher dispatcher, LogApi.LogApiClient client)
    {
        _dispatcher = dispatcher;
        _client = client;
    }

    [HttpGet("logs")]
    [Authorize("staff")]
    [ProducesResponseType(typeof(ApiResponse<PagedData<LogEntry>>), StatusCodes.Status200OK)]
    public Task<IResult> QueryLogs(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] string? from = null,
        [FromQuery] string? to = null,
        [FromQuery] string? service = null,
        [FromQuery] string? eventType = null,
        [FromQuery] string? entityName = null,
        [FromQuery] string? entityId = null,
        [FromQuery] string? actorUserId = null,
        [FromQuery] string? correlationId = null,
        [FromQuery] string? logLevel = null)
    {
        pagination.EnsureValid();
        ValidationHelpers.EnsureGuid(actorUserId, "actorUserId");
        ValidationHelpers.EnsureGuid(correlationId, "correlationId");

        return _dispatcher.DispatchPagedAsync(
            HttpContext,
            "log",
            "logs",
            pagination.Page,
            pagination.PageSize,
            o => _client.QueryLogsAsync(new QueryLogsRequest
            {
                Page = pagination.Page,
                PageSize = pagination.PageSize,
                From = from ?? "",
                To = to ?? "",
                ServiceName = service ?? "",
                EventType = eventType ?? "",
                EntityName = entityName ?? "",
                EntityId = entityId ?? "",
                ActorUserId = actorUserId ?? "",
                CorrelationId = correlationId ?? "",
                LogLevel = logLevel ?? ""
            }, o),
            LogPage.Parser
        );
    }

    [HttpGet("logs/{id:guid}")]
    [Authorize("staff")]
    [ProducesResponseType(typeof(ApiResponse<LogEntry>), StatusCodes.Status200OK)]
    public Task<IResult> GetLog([FromRoute] Guid id)
    {
        return _dispatcher.DispatchAsync(
            HttpContext,
            "log",
            "log-entry",
            o => _client.GetLogAsync(new IdRequest { Id = id.ToString() }, o),
            LogEntry.Parser,
            successMessage: "Log entry retrieved successfully."
        );
    }

    [HttpGet("event-types")]
    [Authorize("staff")]
    [ProducesResponseType(typeof(ApiResponse<LogEventTypeList>), StatusCodes.Status200OK)]
    public Task<IResult> ListLogEventTypes()
    {
        return _dispatcher.DispatchAsync(
            HttpContext,
            "log",
            "log-event-types",
            o => _client.ListLogEventTypesAsync(new Empty(), o),
            LogEventTypeList.Parser,
            successMessage: "Log event types retrieved successfully."
        );
    }

    [HttpPut("event-types/{code}")]
    [Authorize("admin")]
    [ProducesResponseType(typeof(ApiResponse<LogEventType>), StatusCodes.Status200OK)]
    public Task<IResult> SetLogEventTypeEnabled([FromRoute] string code, [FromBody] LogEventTypeToggleRequestDto body)
    {
        return _dispatcher.DispatchAsync(
            HttpContext,
            "log",
            "log-event-type-toggle",
            o => _client.SetLogEventTypeEnabledAsync(new SetLogEventTypeEnabledRequest
            {
                Code = code,
                Enabled = body.Enabled
            }, o),
            LogEventType.Parser,
            successMessage: "Log event type toggled successfully."
        );
    }
}
