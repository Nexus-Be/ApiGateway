using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Nexus.Gateway.Controllers;

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.Gateway.Clients;
using Nexus.Gateway.Dtos.HealthProber;
using Nexus.Gateway.HealthProber;
using Nexus.Gateway.Responses;
using Nexus.Gateway.Security;

[ApiController]
[Route("api/v1")]
[EnableRateLimiting("api")]
[Authorize(GatewayPolicies.GatewayAdmin)]
[DisableMockAuthorizationBypass]
public sealed class HealthProberController : ControllerBase, IActionFilter
{
    private readonly IHealthProberManager _manager;
    private readonly ICheckHealServiceClient _checkHealClient;
    private readonly IApiResponseFactory _factory;
    private readonly IOptionsMonitor<HealthProberOptions> _proberOptions;
    private readonly IConfiguration _configuration;

    public HealthProberController(
        IHealthProberManager manager,
        ICheckHealServiceClient checkHealClient,
        IApiResponseFactory factory,
        IOptionsMonitor<HealthProberOptions> proberOptions,
        IConfiguration configuration)
    {
        _manager = manager;
        _checkHealClient = checkHealClient;
        _factory = factory;
        _proberOptions = proberOptions;
        _configuration = configuration;
    }

    [NonAction]
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var expectedKey = !string.IsNullOrWhiteSpace(_proberOptions.CurrentValue.ApiKey)
            ? _proberOptions.CurrentValue.ApiKey
            : _configuration["CHECKHEAL_API_KEY"];

        if (!string.IsNullOrWhiteSpace(expectedKey))
        {
            if (!TryExtractApiKey(context.HttpContext, out var providedKey) ||
                !string.Equals(providedKey, expectedKey, StringComparison.Ordinal))
            {
                var failure = ApiResponseFactory.Failure(
                    context.HttpContext,
                    "Invalid or missing CheckHeal API key.",
                    StatusCodes.Status401Unauthorized,
                    errorCode: "UNAUTHORIZED");

                context.Result = new ObjectResult(failure)
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
            }
        }
    }

    [NonAction]
    public void OnActionExecuted(ActionExecutedContext context)
    {
    }

    private static bool TryExtractApiKey(HttpContext context, out string apiKey)
    {
        if (context.Request.Headers.TryGetValue("X-Api-Key", out var headerValues) &&
            !string.IsNullOrWhiteSpace(headerValues.FirstOrDefault()))
        {
            apiKey = headerValues.First()!.Trim();
            return true;
        }

        if (context.Request.Headers.TryGetValue("X-CheckHeal-Key", out var chValues) &&
            !string.IsNullOrWhiteSpace(chValues.FirstOrDefault()))
        {
            apiKey = chValues.First()!.Trim();
            return true;
        }

        if (context.Request.Headers.TryGetValue("Authorization", out var authValues) &&
            !string.IsNullOrWhiteSpace(authValues.FirstOrDefault()))
        {
            var authVal = authValues.First()!.Trim();
            if (authVal.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var token = authVal["Bearer ".Length..].Trim();
                if (!string.IsNullOrEmpty(token) && !token.Contains('.')) // exclude JWT
                {
                    apiKey = token;
                    return true;
                }
            }
        }

        apiKey = string.Empty;
        return false;
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

        await _checkHealClient.ToggleServiceAsync(service, body.Enabled);

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
        await _checkHealClient.BulkToggleAsync(body.EnableAll);

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

    /// <summary>
    /// Lấy danh sách trạng thái kiểm tra sức khỏe của toàn bộ cơ sở dữ liệu các service
    /// </summary>
    [HttpGet("gateway/prober/databases/status")]
    public async Task<IResult> GetAllDatabasesStatus(CancellationToken cancellationToken)
    {
        var dbs = await _checkHealClient.GetDatabasesAsync(cancellationToken);
        return _factory.SuccessResult(
            HttpContext,
            dbs,
            "Retrieved health status for all service databases.");
    }

    /// <summary>
    /// Lấy trạng thái kiểm tra cơ sở dữ liệu của một service cụ thể
    /// </summary>
    [HttpGet("gateway/prober/databases/status/{service}")]
    public async Task<IResult> GetDatabaseStatus([FromRoute] string service, CancellationToken cancellationToken)
    {
        var db = await _checkHealClient.GetDatabaseAsync(service, cancellationToken);
        if (db == null)
        {
            return ApiResponseFactory.Failure(
                HttpContext,
                $"Database probe configuration for service '{service}' was not found.",
                StatusCodes.Status404NotFound,
                errorCode: "DATABASE_NOT_FOUND");
        }

        return _factory.SuccessResult(
            HttpContext,
            db,
            $"Retrieved database health status for service '{service}'.");
    }

    /// <summary>
    /// Bật/tắt kiểm tra và truyền chuỗi kết nối cho cơ sở dữ liệu của service
    /// </summary>
    [HttpPut("gateway/prober/databases/{service}")]
    public async Task<IResult> ToggleDatabaseProber(
        [FromRoute] string service,
        [FromBody] DatabaseProberToggleDto body,
        CancellationToken cancellationToken)
    {
        var updated = await _checkHealClient.ToggleDatabaseAsync(service, body.Enabled, body.ConnectionString, cancellationToken);
        if (updated == null)
        {
            return ApiResponseFactory.Failure(
                HttpContext,
                $"Failed to toggle database prober for service '{service}'.",
                StatusCodes.Status400BadRequest,
                errorCode: "INVALID_DATABASE_TARGET");
        }

        var action = body.Enabled ? "enabled" : "disabled";
        return _factory.SuccessResult(
            HttpContext,
            updated,
            $"Database prober for service '{service}' has been {action}.");
    }

    /// <summary>
    /// Kích hoạt kiểm tra sức khỏe thủ công (Keep-alive SELECT 1) cho cơ sở dữ liệu của service
    /// </summary>
    [HttpPost("gateway/prober/databases/{service}/check")]
    public async Task<IResult> ManualDatabaseCheck(
        [FromRoute] string service,
        CancellationToken cancellationToken)
    {
        var result = await _checkHealClient.CheckDatabaseAsync(service, cancellationToken);
        if (result == null)
        {
            return ApiResponseFactory.Failure(
                HttpContext,
                $"Database check execution failed for service '{service}'.",
                StatusCodes.Status400BadRequest,
                errorCode: "DATABASE_CHECK_FAILED");
        }

        var message = result.Status == "healthy"
            ? $"Database for service '{service}' is Healthy. Latency: {result.LatencyMs}ms."
            : $"Database for service '{service}' is Unhealthy: {result.Error ?? result.Message}. Latency: {result.LatencyMs}ms.";

        return _factory.SuccessResult(HttpContext, result, message);
    }

    /// <summary>
    /// Kiểm tra trạng thái hiện tại của chính service CheckHeal
    /// </summary>
    [HttpGet("gateway/prober/healthcheck-service/status")]
    public async Task<IResult> GetCheckHealServiceStatus(CancellationToken cancellationToken)
    {
        var status = await _checkHealClient.CheckHealthCheckServiceAsync(cancellationToken);
        return _factory.SuccessResult(
            HttpContext,
            status,
            "Retrieved CheckHealService operational status.");
    }

    /// <summary>
    /// Kích hoạt chủ động probe kiểm tra sức khỏe service CheckHeal từ Gateway
    /// </summary>
    [HttpPost("gateway/prober/healthcheck-service/check")]
    public async Task<IResult> CheckHealthCheckService(CancellationToken cancellationToken)
    {
        var sample = await _checkHealClient.CheckHealthCheckServiceAsync(cancellationToken);
        var message = sample.IsHealthy
            ? $"CheckHealService is Healthy. Latency: {sample.LatencyMs}ms."
            : $"CheckHealService is Unhealthy: {sample.Details}. Latency: {sample.LatencyMs}ms.";

        return _factory.SuccessResult(HttpContext, sample, message);
    }
}
