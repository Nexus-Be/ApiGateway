namespace Nexus.Gateway.Controllers;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nexus.Gateway.Dispatching;

[ApiController]
public abstract class GatewayBaseController : ControllerBase
{
    protected readonly IGatewayDispatcher Dispatcher;

    protected GatewayBaseController(IGatewayDispatcher dispatcher)
    {
        Dispatcher = dispatcher;
    }

    protected Task<IResult> ForwardAsync(RestForwardRequest request)
    {
        return Dispatcher.DispatchRestAsync(HttpContext, request);
    }

    protected Task<IResult> ForwardPagedAsync(RestPagedForwardRequest request)
    {
        return Dispatcher.DispatchRestPagedAsync(HttpContext, request);
    }

    protected Task<IResult> ForwardNoContentAsync(RestForwardRequest request)
    {
        return Dispatcher.DispatchRestNoContentAsync(HttpContext, request);
    }

    protected Task<IResult> ForwardAsync(
        string service,
        string downstreamPath,
        HttpMethod method,
        object? body = null,
        string? mockSeed = null,
        string? successMessage = null,
        int successStatusCode = StatusCodes.Status200OK,
        IDictionary<string, string?>? queryParams = null)
    {
        return Dispatcher.DispatchRestAsync(HttpContext, new RestForwardRequest
        {
            Service = service,
            DownstreamPath = downstreamPath,
            Method = method,
            Body = body,
            MockSeed = mockSeed,
            SuccessMessage = successMessage,
            SuccessStatusCode = successStatusCode,
            QueryParams = queryParams
        });
    }

    protected Task<IResult> ForwardPagedAsync(
        string service,
        string downstreamPath,
        int page,
        int pageSize,
        HttpMethod? method = null,
        object? body = null,
        string? mockSeed = null,
        int successStatusCode = StatusCodes.Status200OK,
        IDictionary<string, string?>? queryParams = null)
    {
        return Dispatcher.DispatchRestPagedAsync(HttpContext, new RestPagedForwardRequest
        {
            Service = service,
            DownstreamPath = downstreamPath,
            Method = method ?? HttpMethod.Get,
            Body = body,
            MockSeed = mockSeed,
            Page = page,
            PageSize = pageSize,
            SuccessStatusCode = successStatusCode,
            QueryParams = queryParams
        });
    }

    protected Task<IResult> ForwardNoContentAsync(
        string service,
        string downstreamPath,
        HttpMethod method,
        object? body = null,
        string? mockSeed = null,
        IDictionary<string, string?>? queryParams = null)
    {
        return Dispatcher.DispatchRestNoContentAsync(HttpContext, new RestForwardRequest
        {
            Service = service,
            DownstreamPath = downstreamPath,
            Method = method,
            Body = body,
            MockSeed = mockSeed,
            QueryParams = queryParams
        });
    }
}
