namespace Nexus.Gateway.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.Gateway.Common;
using Nexus.Gateway.Dispatching;
using Nexus.Gateway.Dtos.Audit;
using Nexus.Gateway.Responses;
using Nexus.Gateway.Security;
using ShoppingCart.Contracts.V1;

[ApiController]
[Route("gateway/api/v1/audit")]
[BackendTag("audit")]
[EnableRateLimiting("api")]
public sealed class AuditController : ControllerBase
{
    private readonly IGatewayDispatcher _dispatcher;
    private readonly AuditApi.AuditApiClient _client;

    public AuditController(IGatewayDispatcher dispatcher, AuditApi.AuditApiClient client)
    {
        _dispatcher = dispatcher;
        _client = client;
    }

    [HttpGet("logs")]
    [Authorize("staff")]
    [ProducesResponseType(typeof(ApiResponse<PagedData<AuditLog>>), StatusCodes.Status200OK)]
    public Task<IResult> QueryAuditLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? from = null,
        [FromQuery] string? to = null,
        [FromQuery] string? service = null,
        [FromQuery] string? eventType = null,
        [FromQuery] string? entityName = null,
        [FromQuery] string? entityId = null,
        [FromQuery] string? actorUserId = null,
        [FromQuery] string? correlationId = null)
    {
        if (!ValidationHelpers.PageOk(page, pageSize))
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, ValidationHelpers.PageMessage, field: "Pagination"));
        if (!ValidationHelpers.GuidOk(actorUserId) || !ValidationHelpers.GuidOk(correlationId))
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "actorUserId/correlationId phải là UUID"));

        return _dispatcher.DispatchPagedAsync(
            HttpContext,
            "audit",
            "audit-logs",
            page,
            pageSize,
            o => _client.QueryAuditLogsAsync(new QueryAuditLogsRequest
            {
                Page = page,
                PageSize = pageSize,
                From = from ?? "",
                To = to ?? "",
                ServiceName = service ?? "",
                EventType = eventType ?? "",
                EntityName = entityName ?? "",
                EntityId = entityId ?? "",
                ActorUserId = actorUserId ?? "",
                CorrelationId = correlationId ?? ""
            }, o),
            AuditLogPage.Parser
        );
    }

    [HttpGet("logs/{id:guid}")]
    [Authorize("staff")]
    [ProducesResponseType(typeof(ApiResponse<AuditLog>), StatusCodes.Status200OK)]
    public Task<IResult> GetAuditLog([FromRoute] Guid id)
    {
        return _dispatcher.DispatchAsync(
            HttpContext,
            "audit",
            "audit-log",
            o => _client.GetAuditLogAsync(new IdRequest { Id = id.ToString() }, o),
            AuditLog.Parser,
            successMessage: "Audit log retrieved successfully."
        );
    }

    [HttpGet("event-types")]
    [Authorize("staff")]
    [ProducesResponseType(typeof(ApiResponse<AuditEventTypeList>), StatusCodes.Status200OK)]
    public Task<IResult> ListAuditEventTypes()
    {
        return _dispatcher.DispatchAsync(
            HttpContext,
            "audit",
            "audit-event-types",
            o => _client.ListAuditEventTypesAsync(new Empty(), o),
            AuditEventTypeList.Parser,
            successMessage: "Audit event types retrieved successfully."
        );
    }

    [HttpPut("event-types/{code}")]
    [Authorize("admin")]
    [ProducesResponseType(typeof(ApiResponse<AuditEventType>), StatusCodes.Status200OK)]
    public Task<IResult> SetAuditEventTypeEnabled([FromRoute] string code, [FromBody] AuditEventTypeToggleRequestDto body)
    {
        return _dispatcher.DispatchAsync(
            HttpContext,
            "audit",
            "audit-event-type-toggle",
            o => _client.SetAuditEventTypeEnabledAsync(new SetAuditEventTypeEnabledRequest
            {
                Code = code,
                Enabled = body.Enabled
            }, o),
            AuditEventType.Parser,
            successMessage: "Audit event type toggled successfully."
        );
    }
}
