using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexus.Gateway.Common;
using Nexus.Gateway.Responses;
using Nexus.Gateway.Security;
using Nexus.Gateway.Services;

namespace Nexus.Gateway.MockEngine;

public sealed class MockEngineMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MockEngineMiddleware> _logger;

    public MockEngineMiddleware(RequestDelegate _next, ILogger<MockEngineMiddleware> logger)
    {
        this._next = _next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IMockEngine mockEngine,
        IGatewayRuntimeManager runtimeManager,
        IOptionsMonitor<GatewayOptions> options,
        IAuthorizationService authorizationService,
        IApiResponseFactory responseFactory)
    {
        var match = GatewayRequestResolution.Get(context)?.MockMatch
            ?? mockEngine.Match(context.Request.Method, context.Request.Path);
        if (!match.IsMatch || match.Definition is null)
        {
            await _next(context);
            return;
        }

        var def = match.Definition;
        var serviceConfig = options.CurrentValue.GetService(def.Service);
        var configuredMode = serviceConfig?.Mode ?? "Mock";
        var effectiveMode = GatewayRequestResolution.Get(context) is { } resolution
            ? resolution.EffectiveMode(context, def.Service, options.CurrentValue, runtimeManager)
            : runtimeManager.ResolveEffectiveMode(context, def.Service, configuredMode);

        // If service is in Real mode or already handled by a C# MVC Controller,
        // let the request pass through to the controller for model binding and validation
        if (effectiveMode == "Real" || context.GetEndpoint()?.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>() != null)
        {
            await _next(context);
            return;
        }

        _logger.LogInformation("MockEngine matched route: [{Method}] {Path} => Mock ID: {Id} (Service: {Service})",
            def.Method, def.Path, def.Id, def.Service);

        context.Response.Headers["X-Gateway-Mode"] = "Mock";
        context.Response.Headers.CacheControl = "no-store";

        // Validate Security Policy
        var authResult = await AuthorizeMockRequestAsync(context, def.Policy, runtimeManager, authorizationService, responseFactory);
        if (authResult is not null)
        {
            await authResult.ExecuteAsync(context);
            return;
        }

        // Handle Response Formatting
        var statusCode = def.StatusCode;
        var responseType = def.ResponseType.ToLowerInvariant();

        if (responseType == "raw")
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            if (def.Response.HasValue)
            {
                await context.Response.WriteAsync(def.Response.Value.GetRawText());
            }
            return;
        }

        if (responseType == "paged")
        {
            var pageIndex = 1;
            var pageSize = 20;

            var invalidPage = context.Request.Query.TryGetValue("page", out var requestedPage) &&
                              (!int.TryParse(requestedPage, out pageIndex) || pageIndex < 1);
            var invalidSize = context.Request.Query.TryGetValue("pageSize", out var requestedPageSize) &&
                              (!int.TryParse(requestedPageSize, out pageSize) || pageSize is < 1 or > 100);
            if (invalidPage || invalidSize || !ValidationHelpers.PageOk(pageIndex, pageSize))
            {
                await responseFactory.FailureResult(context, "One or more validation errors occurred.",
                    StatusCodes.Status400BadRequest, ValidationHelpers.PageMessage, "VAL_INVALID_INPUT",
                    errors: new Dictionary<string, string[]> { ["Pagination"] = [ValidationHelpers.PageMessage] })
                    .ExecuteAsync(context);
                return;
            }

            var itemsList = new List<JsonElement>();
            var totalCount = 0;

            if (def.Response.HasValue)
            {
                var root = def.Response.Value;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("items", out var itemsElem) && itemsElem.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in itemsElem.EnumerateArray())
                        {
                            itemsList.Add(item.Clone());
                        }
                    }

                    if (root.TryGetProperty("total", out var totalElem) && totalElem.TryGetInt32(out var t))
                    {
                        totalCount = t;
                    }
                    else
                    {
                        totalCount = itemsList.Count;
                    }
                }
                else if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in root.EnumerateArray())
                    {
                        itemsList.Add(item.Clone());
                    }
                    totalCount = itemsList.Count;
                }
            }

            var pagedResult = responseFactory.PagedResult(context, itemsList, pageIndex, pageSize, totalCount, statusCode);
            await pagedResult.ExecuteAsync(context);
            return;
        }

        if (responseType == "error")
        {
            var errorResult = responseFactory.FailureResult(
                context,
                title: def.Message ?? "Mock error response",
                status: statusCode,
                detail: def.Description,
                errorCode: def.ErrorCode ?? "MOCK_ERROR"
            );
            await errorResult.ExecuteAsync(context);
            return;
        }

        // Default: Single response
        var data = def.Response.HasValue ? def.Response.Value : default;
        var successResult = responseFactory.SuccessResult(context, data, def.Message, statusCode);
        await successResult.ExecuteAsync(context);
    }

    private static async Task<IResult?> AuthorizeMockRequestAsync(
        HttpContext context,
        string policy,
        IGatewayRuntimeManager runtimeManager,
        IAuthorizationService authorizationService,
        IApiResponseFactory responseFactory)
    {
        var normalizedPolicy = policy.Trim().ToLowerInvariant();

        if (normalizedPolicy is "" or "public" or "anonymous")
        {
            return null;
        }

        // Bypass is intentionally limited to Development by the runtime manager.
        // It can never apply to endpoints carrying DisableMockAuthorizationBypass.
        if (runtimeManager.IsAuthBypassed(context) && context.AllowsMockAuthorizationBypass())
        {
            return null;
        }

        var authorized = await authorizationService.AuthorizeAsync(context.User, normalizedPolicy);
        if (authorized.Succeeded)
        {
            return null;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            return responseFactory.FailureResult(
                context,
                title: "Authentication required",
                status: StatusCodes.Status401Unauthorized,
                detail: "A valid Bearer token is required to access this resource.",
                errorCode: "AUTH_UNAUTHORIZED",
                type: "https://tools.ietf.org/html/rfc9110#section-15.5.2"
            );
        }

        return responseFactory.FailureResult(
            context,
            title: "Access denied",
            status: StatusCodes.Status403Forbidden,
            detail: $"Policy '{policy}' requires higher privilege.",
            errorCode: "AUTH_FORBIDDEN",
            type: "https://tools.ietf.org/html/rfc9110#section-15.5.4"
        );
    }
}

public static class MockEngineMiddlewareExtensions
{
    public static IApplicationBuilder UseMockEngine(this IApplicationBuilder app)
    {
        return app.UseMiddleware<MockEngineMiddleware>();
    }
}
