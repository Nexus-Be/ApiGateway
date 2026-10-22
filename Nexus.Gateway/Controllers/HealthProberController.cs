namespace Nexus.Gateway.Controllers;

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.Gateway.Dtos.HealthProber;
using Nexus.Gateway.HealthProber;
using Nexus.Gateway.Responses;
using Nexus.Gateway.Security;

[ApiController]
[Route("api/v1")]
[EnableRateLimiting("api")]
[Authorize(GatewayPolicies.GatewayAdmin)]
[DisableMockAuthorizationBypass]
public sealed class HealthProberController : ControllerBase
{
    private readonly IHealthProberManager _manager;
    private readonly IApiResponseFactory _factory;

    public HealthProberController(
        IHealthProberManager manager,
        IApiResponseFactory factory)
    {
        _manager = manager;
        _factory = factory;
    }

    [HttpGet("gateway/prober/status")]
    [ProducesResponseType(typeof(ApiResponse<ServiceHealthStatusDto[]>), StatusCodes.Status200OK)]
    public IResult GetAllStatus()
    {
        var states = _manager.GetAllStates().Select(s => new ServiceHealthStatusDto(
            Service: s.ServiceName,
            IsEnabled: s.IsEnabled,
            Status: s.Status.ToString(),
            ConsecutiveFailures: s.ConsecutiveFailures,
            AutoRecoveryCount: s.AutoRecoveryCount,
            LastCheckTime: s.LastCheckTime,
            LastError: s.LastError,
            RecentSamples: s.GetRecentSamples()
        )).ToArray();

        return _factory.SuccessResult(HttpContext, states, "Retrieved health prober status for all services.");
    }

    [HttpGet("gateway/prober/status/{service}")]
    [ProducesResponseType(typeof(ApiResponse<ServiceHealthStatusDto>), StatusCodes.Status200OK)]
    public IResult GetServiceStatus([FromRoute] string service)
    {
        var state = _manager.GetState(service);
        if (state == null)
        {
            return ApiResponseFactory.Failure(
                HttpContext,
                $"Service '{service}' is not recognized.",
                StatusCodes.Status404NotFound,
                errorCode: "SERVICE_NOT_FOUND");
        }

        var dto = new ServiceHealthStatusDto(
            Service: state.ServiceName,
            IsEnabled: state.IsEnabled,
            Status: state.Status.ToString(),
            ConsecutiveFailures: state.ConsecutiveFailures,
            AutoRecoveryCount: state.AutoRecoveryCount,
            LastCheckTime: state.LastCheckTime,
            LastError: state.LastError,
            RecentSamples: state.GetRecentSamples()
        );

        return _factory.SuccessResult(HttpContext, dto, $"Retrieved health status for service '{service}'.");
    }

    [HttpPut("gateway/prober/{service}")]
    public async Task<IResult> ToggleServiceProber(
        [FromRoute] string service,
        [FromBody] ServiceProberToggleDto body)
    {
        bool success;
        if (body.Enabled)
        {
            success = await _manager.EnableServiceProberAsync(service, body.IntervalSeconds, body.FailureThreshold);
        }
        else
        {
            success = await _manager.DisableServiceProberAsync(service);
        }

        if (!success)
        {
            return ApiResponseFactory.Failure(
                HttpContext,
                $"Failed to toggle prober for service '{service}'. Service name may be invalid.",
                StatusCodes.Status400BadRequest,
                errorCode: "INVALID_SERVICE_NAME");
        }

        var action = body.Enabled ? "enabled" : "disabled";
        return _factory.SuccessResult(
            HttpContext,
            new { service, enabled = body.Enabled, status = _manager.GetStatus(service).ToString() },
            $"Health prober for service '{service}' has been {action}.");
    }

    [HttpPut("gateway/prober/bulk")]
    public async Task<IResult> ToggleBulkProber([FromBody] BulkProberToggleDto body)
    {
        await _manager.SetBulkProberAsync(body.EnableAll);
        var action = body.EnableAll ? "enabled" : "disabled";
        return _factory.SuccessResult(
            HttpContext,
            new { enabledAll = body.EnableAll },
            $"Health prober for all canonical services has been {action}.");
    }

    /// <summary>
    /// API kiểm tra sức khỏe thủ công tức thì (Manual Health Check on Demand)
    /// </summary>
    [HttpPost("gateway/prober/{service}/check")]
    [ProducesResponseType(typeof(ApiResponse<HealthCheckSample>), StatusCodes.Status200OK)]
    public async Task<IResult> ManualHealthCheck(
        [FromRoute] string service,
        CancellationToken cancellationToken)
    {
        var canonical = GatewayOptions.ResolveCanonicalName(service);
        if (!GatewayOptions.CanonicalServices.Contains(canonical))
        {
            return ApiResponseFactory.Failure(
                HttpContext,
                $"Service '{service}' is not a valid canonical service.",
                StatusCodes.Status404NotFound,
                errorCode: "SERVICE_NOT_FOUND");
        }

        var sample = await _manager.ProbeOnceAsync(canonical, cancellationToken);
        var message = sample.IsSuccess
            ? $"Service '{canonical}' is Healthy. Latency: {sample.LatencyMs}ms."
            : $"Service '{canonical}' is Unhealthy: {sample.ErrorMessage}. Latency: {sample.LatencyMs}ms.";

        return _factory.SuccessResult(HttpContext, sample, message);
    }
}
