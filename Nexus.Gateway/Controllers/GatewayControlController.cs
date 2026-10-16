namespace Nexus.Gateway.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.Gateway.Dtos.GatewayControl;
using Nexus.Gateway.MockEngine;
using Nexus.Gateway.Responses;
using Nexus.Gateway.Security;
using Nexus.Gateway.Services;

[ApiController]
[EnableRateLimiting("api")]
[Authorize(GatewayPolicies.GatewayAdmin)]
[DisableMockAuthorizationBypass]
public sealed class GatewayControlController : ControllerBase
{
    private readonly IGatewayRuntimeManager _manager;
    private readonly IApiResponseFactory _factory;
    private readonly IMockEngine _engine;

    public GatewayControlController(
        IGatewayRuntimeManager manager,
        IApiResponseFactory factory,
        IMockEngine engine)
    {
        _manager = manager;
        _factory = factory;
        _engine = engine;
    }

    [HttpGet("gateway/api/v1/gateway/modes")]
    [ProducesResponseType(typeof(ApiResponse<GatewayStateResponseDto>), StatusCodes.Status200OK)]
    public IResult GetModes()
    {
        var state = new GatewayStateResponseDto(_manager.GetAuthMode(HttpContext), _manager.GetServiceModes());
        return _factory.SuccessResult(HttpContext, state, "Gateway service modes retrieved.");
    }

    [HttpPut("gateway/api/v1/gateway/mode")]
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

    [HttpPut("gateway/api/v1/gateway/modes/{service}")]
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

    [HttpPut("gateway/api/v1/gateway/modes")]
    public async Task<IResult> UpdateBulkModes([FromBody] BulkServiceModeUpdateRequestDto body)
    {
        var requestedAuthMode = body.AuthMode ?? body.GatewayMode;
        if (!string.IsNullOrEmpty(requestedAuthMode) && !await _manager.SetAuthModeAsync(requestedAuthMode))
        {
            return ApiResponseFactory.Failure(
                HttpContext,
                "Invalid auth mode. Must be 'Enforce' or Development-only 'Bypass'.",
                StatusCodes.Status400BadRequest,
                errorCode: "INVALID_AUTH_MODE"
            );
        }
        if (body.Services != null && !await _manager.SetBulkServiceModesAsync(body.Services))
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

    [HttpGet("gateway/api/v1/mock-engine/definitions")]
    public IResult GetMockDefinitions()
    {
        var defs = _engine.GetDefinitions().Select(d => new
        {
            d.Id,
            d.Service,
            d.Method,
            d.Path,
            d.Description,
            d.Policy,
            d.StatusCode,
            d.ResponseType,
            hasRequest = d.Request.HasValue,
            hasResponse = d.Response.HasValue
        });
        return _factory.SuccessResult(HttpContext, defs, "Mock endpoint definitions retrieved.");
    }

    [HttpPost("gateway/api/v1/mock-engine/reload")]
    public IResult ReloadMockDefinitions()
    {
        _engine.Reload();
        var count = _engine.GetDefinitions().Count;
        return _factory.SuccessResult(HttpContext, new { loadedCount = count }, $"Reloaded {count} mock definitions successfully.");
    }
}
