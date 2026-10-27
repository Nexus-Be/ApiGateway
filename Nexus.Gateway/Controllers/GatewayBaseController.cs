namespace Nexus.Gateway.Controllers;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nexus.Gateway.Dispatching;

/// <summary>
/// Base class for all API Gateway Controllers.
/// Provides standardized, one-line helper methods for forwarding HTTP requests to downstream microservices
/// in Real mode, or serving mocked responses from seed files in Mock mode.
/// </summary>
[ApiController]
public abstract class GatewayBaseController : ControllerBase
{
    protected readonly IGatewayDispatcher Dispatcher;

    protected GatewayBaseController(IGatewayDispatcher dispatcher)
    {
        Dispatcher = dispatcher;
    }

    /// <summary>
    /// Forwards a request using a fully configured <see cref="RestForwardRequest"/>.
    /// </summary>
    protected Task<IResult> ForwardAsync(RestForwardRequest request)
    {
        return Dispatcher.DispatchRestAsync(HttpContext, request);
    }

    /// <summary>
    /// Forwards a paginated request using a fully configured <see cref="RestPagedForwardRequest"/>.
    /// Automatically extracts items and pagination metadata and formats into ApiResponse&lt;PagedData&lt;T&gt;&gt;.
    /// </summary>
    protected Task<IResult> ForwardPagedAsync(RestPagedForwardRequest request)
    {
        return Dispatcher.DispatchRestPagedAsync(HttpContext, request);
    }

    /// <summary>
    /// Forwards a request that returns 204 No Content upon success (e.g., DELETE or empty PUT).
    /// </summary>
    protected Task<IResult> ForwardNoContentAsync(RestForwardRequest request)
    {
        return Dispatcher.DispatchRestNoContentAsync(HttpContext, request);
    }

    /// <summary>
    /// Convenient one-liner helper to forward standard requests.
    /// </summary>
    /// <param name="service">Canonical service name (e.g. "catalog", "users", "commerce")</param>
    /// <param name="downstreamPath">Relative downstream REST route (e.g. "/api/v1/products")</param>
    /// <param name="method">HTTP method (GET, POST, PUT, DELETE, PATCH)</param>
    /// <param name="body">Request payload DTO to serialize</param>
    /// <param name="mockSeed">Seed fixture name without .json extension (defaults to route name)</param>
    /// <param name="successMessage">Optional envelope success message</param>
    /// <param name="successStatusCode">HTTP status code on success (default 200 OK)</param>
    /// <param name="queryParams">Optional query parameters dictionary</param>
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

    /// <summary>
    /// Forwards a request with optional gRPC fallback for services undergoing transition.
    /// In Mock mode, serves from seed fixture.
    /// In Real mode, calls gRPC if grpcCall is provided, otherwise calls HTTP REST downstream.
    /// </summary>
    protected Task<IResult> ForwardAsync<TResponse>(
        string service,
        string downstreamPath,
        HttpMethod method,
        Func<CallOptions, AsyncUnaryCall<TResponse>>? grpcCall = null,
        MessageParser<TResponse>? parser = null,
        object? body = null,
        string? mockSeed = null,
        string? successMessage = null,
        int successStatusCode = StatusCodes.Status200OK,
        IDictionary<string, string?>? queryParams = null)
        where TResponse : class, IMessage<TResponse>
    {
        if (grpcCall != null && parser != null)
        {
            var seed = mockSeed ?? downstreamPath.Trim('/').Replace('/', '-');
            return Dispatcher.DispatchAsync(
                HttpContext,
                service,
                seed,
                grpcCall,
                parser,
                successMessage,
                successStatusCode
            );
        }

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

    /// <summary>
    /// Convenient one-liner helper to forward paginated requests.
    /// </summary>
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

    /// <summary>
    /// Forwards a paginated request with optional gRPC fallback.
    /// </summary>
    protected Task<IResult> ForwardPagedAsync<TResponse>(
        string service,
        string downstreamPath,
        int page,
        int pageSize,
        Func<CallOptions, AsyncUnaryCall<TResponse>>? grpcCall = null,
        MessageParser<TResponse>? parser = null,
        HttpMethod? method = null,
        object? body = null,
        string? mockSeed = null,
        int successStatusCode = StatusCodes.Status200OK,
        IDictionary<string, string?>? queryParams = null)
        where TResponse : class, IMessage<TResponse>
    {
        if (grpcCall != null && parser != null)
        {
            var seed = mockSeed ?? downstreamPath.Trim('/').Replace('/', '-');
            return Dispatcher.DispatchPagedAsync(
                HttpContext,
                service,
                seed,
                page,
                pageSize,
                grpcCall,
                parser
            );
        }

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

    /// <summary>
    /// Convenient one-liner helper to forward requests expecting 204 No Content.
    /// </summary>
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

    /// <summary>
    /// Forwards a request expecting 204 No Content with optional gRPC fallback.
    /// </summary>
    protected Task<IResult> ForwardNoContentAsync<TResponse>(
        string service,
        string downstreamPath,
        HttpMethod method,
        Func<CallOptions, AsyncUnaryCall<TResponse>>? grpcCall = null,
        MessageParser<TResponse>? parser = null,
        object? body = null,
        string? mockSeed = null,
        IDictionary<string, string?>? queryParams = null)
        where TResponse : class, IMessage<TResponse>
    {
        if (grpcCall != null && parser != null)
        {
            var seed = mockSeed ?? downstreamPath.Trim('/').Replace('/', '-');
            return Dispatcher.DispatchNoContentAsync(
                HttpContext,
                service,
                seed,
                grpcCall,
                parser
            );
        }

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
