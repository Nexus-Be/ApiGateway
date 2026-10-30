namespace Nexus.Gateway.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.Gateway.Dtos.GatewayControl;
using Nexus.Gateway.Responses;
using Nexus.Gateway.Security;
using Nexus.Gateway.Services;

[ApiController]
[Route("api/v1")]
[EnableRateLimiting("api")]
[Authorize(GatewayPolicies.GatewayAdmin)]
[DisableMockAuthorizationBypass]
[BackendTag("checkheal")]
public sealed class GatewayControlController : ControllerBase
{
    private readonly IGatewayRuntimeManager _manager;
    private readonly IApiResponseFactory _factory;

    public GatewayControlController(
        IGatewayRuntimeManager manager,
        IApiResponseFactory factory)
    {
        _manager = manager;
        _factory = factory;
    }

    [HttpGet("gateway/modes")]
    [ProducesResponseType(typeof(ApiResponse<GatewayStateResponseDto>), StatusCodes.Status200OK)]
    public IResult GetModes()
    {
        var state = new GatewayStateResponseDto(_manager.GetAuthMode(HttpContext), _manager.GetServiceModes());
        return _factory.SuccessResult(HttpContext, state, "Gateway service modes retrieved.");
    }

    [HttpPut("gateway/mode")]
    public async Task<IResult> UpdateAuthMode([FromBody] AuthModeUpdateRequestDto body)
    {
        var success = await _manager.SetAuthModeAsync(body.Mode);
        if (!success)
        {
            return ApiResponseFactory.Failure(
                HttpContext,
                "Invalid auth mode. Must be 'Enforce' or Development-only 'Bypass'.",
                StatusCodes.Status400BadRequest,
                errorCode: "INVALID_AUTH_MODE"
            );
        }
        return _factory.SuccessResult(HttpContext, new { authMode = _manager.GetAuthMode(HttpContext) }, $"Gateway auth mode updated to '{body.Mode}'.");
    }

    [HttpPut("gateway/modes/{service}")]
    public async Task<IResult> UpdateServiceMode([FromRoute] string service, [FromBody] ServiceModeUpdateRequestDto body)
    {
        var success = await _manager.SetServiceModeAsync(service, body.Mode);
        if (!success)
        {
            return ApiResponseFactory.Failure(
                HttpContext,
                "Invalid service mode. Must be 'Mock' or 'Real'.",
                StatusCodes.Status400BadRequest,
                errorCode: "INVALID_MODE"
            );
        }
        return _factory.SuccessResult(HttpContext, new { service, mode = body.Mode }, $"Service '{service}' mode updated to '{body.Mode}'.");
    }

    [HttpPut("gateway/modes")]
    public async Task<IResult> UpdateBulkModes([FromBody] BulkServiceModeUpdateRequestDto body)
    {
        var requestedAuthMode = body.AuthMode ?? body.GatewayMode;
        var result = await _manager.SetBulkModesAsync(requestedAuthMode, body.Services);
        if (result == BulkModesUpdateResult.InvalidAuthMode)
        {
            return ApiResponseFactory.Failure(
                HttpContext,
                "Invalid auth mode. Must be 'Enforce' or Development-only 'Bypass'.",
                StatusCodes.Status400BadRequest,
                errorCode: "INVALID_AUTH_MODE"
            );
        }
        if (result == BulkModesUpdateResult.InvalidServiceMode)
        {
            return ApiResponseFactory.Failure(
                HttpContext,
                "One or more service names or modes are invalid.",
                StatusCodes.Status400BadRequest,
                errorCode: "INVALID_SERVICE_MODE"
            );
        }
        var state = new GatewayStateResponseDto(_manager.GetAuthMode(HttpContext), _manager.GetServiceModes());
        return _factory.SuccessResult(HttpContext, state, "Gateway modes updated.");
    }
}
