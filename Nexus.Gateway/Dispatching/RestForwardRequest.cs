namespace Nexus.Gateway.Dispatching;

using System.Collections.Generic;
using System.Net.Http;
using Microsoft.AspNetCore.Http;

public sealed class RestForwardRequest
{
    public required string Service { get; init; }
    public required string DownstreamPath { get; init; }
    public HttpMethod Method { get; init; } = HttpMethod.Get;
    public object? Body { get; init; }
    public string? MockSeed { get; init; }
    public string? SuccessMessage { get; init; }
    public int SuccessStatusCode { get; init; } = StatusCodes.Status200OK;
    public IDictionary<string, string?>? QueryParams { get; init; }
}

public sealed class RestPagedForwardRequest
{
    public required string Service { get; init; }
    public required string DownstreamPath { get; init; }
    public HttpMethod Method { get; init; } = HttpMethod.Get;
    public object? Body { get; init; }
    public string? MockSeed { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public int SuccessStatusCode { get; init; } = StatusCodes.Status200OK;
    public IDictionary<string, string?>? QueryParams { get; init; }
}
