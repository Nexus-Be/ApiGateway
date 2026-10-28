namespace Nexus.Gateway.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.Gateway.Common;
using Nexus.Gateway.Dispatching;
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
}
