namespace Nexus.Gateway.Controllers;

using Microsoft.AspNetCore.Mvc;
using Nexus.Gateway.Common;
using Nexus.Gateway.Dispatching;
using Nexus.Gateway.Security;

/// <summary>
/// Log surface (specs/02_GATEWAY_CONTROLLERS_ROUTING.md §14).
/// Public audit and event logging are handled per flow_mock_excel.xlsx via:
/// - AdminUsersController: /api/v1/admin/audit-logs (Row 60)
/// - NotificationController: /api/v1/admin/event-logs (Row 195)
/// - NotificationController: /api/v1/admin/event-logs/{id} (Row 196)
/// - NotificationController: /api/v1/admin/health/broker (Row 197)
/// </summary>
[ApiController]
[Route("api/v1/log")]
[BackendTag("log")]
public sealed class LogController : GatewayBaseController
{
    public LogController(IGatewayDispatcher dispatcher) : base(dispatcher)
    {
    }
}
