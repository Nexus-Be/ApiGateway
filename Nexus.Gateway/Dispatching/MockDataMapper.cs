namespace Nexus.Gateway.Dispatching;

using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Nexus.Gateway.Responses;

public interface IMockDataMapper
{
    Task<(JsonElement? Data, IResult? Failure)> MapMockAsync(
        HttpContext context,
        string service,
        string operation,
        BackendOptions config,
        CancellationToken cancellationToken);

    Task<(string? JsonString, IResult? Failure)> MapMockPagedJsonAsync(
        HttpContext context,
        string service,
        string operation,
        BackendOptions config,
        CancellationToken cancellationToken);
}

public sealed class MockDataMapper : IMockDataMapper
{
    private readonly IWebHostEnvironment _environment;
    private readonly IApiResponseFactory _responseFactory;

    public MockDataMapper(IWebHostEnvironment environment, IApiResponseFactory responseFactory)
    {
        _environment = environment;
        _responseFactory = responseFactory;
    }

    public async Task<(JsonElement? Data, IResult? Failure)> MapMockAsync(
        HttpContext context,
        string service,
        string operation,
        BackendOptions config,
        CancellationToken cancellationToken)
    {
        if (config.MockDelayMs > 0)
        {
            await Task.Delay(config.MockDelayMs, cancellationToken);
        }

        if (config.MockStatus != StatusCodes.Status200OK)
        {
            var failure = _responseFactory.FailureResult(
                context,
                title: "Configured mock failure",
                status: config.MockStatus,
                detail: $"Mock returned configured failure status {config.MockStatus}.",
                errorCode: "MOCK_FAILURE"
            );
            return (null, failure);
        }

        var fixturePath = ResolveSeedFilePath(service, operation);
        if (fixturePath == null || !File.Exists(fixturePath))
        {
            var missing = _responseFactory.FailureResult(
                context,
                title: "Seed data not found",
                status: StatusCodes.Status500InternalServerError,
                detail: $"Seed data file for '{service}/{operation}' is missing. Expected at: Seeds/{service}/{operation}.json or Seeds/{operation}/{operation}.json",
                errorCode: "SEED_DATA_NOT_FOUND"
            );
            return (null, missing);
        }

        var json = await File.ReadAllTextAsync(fixturePath, cancellationToken);
        using var doc = JsonDocument.Parse(json);
        return (doc.RootElement.Clone(), null);
    }

    public async Task<(string? JsonString, IResult? Failure)> MapMockPagedJsonAsync(
        HttpContext context,
        string service,
        string operation,
        BackendOptions config,
        CancellationToken cancellationToken)
    {
        if (config.MockDelayMs > 0)
        {
            await Task.Delay(config.MockDelayMs, cancellationToken);
        }

        if (config.MockStatus != StatusCodes.Status200OK)
        {
            var failure = _responseFactory.FailureResult(
                context,
                title: "Configured mock failure",
                status: config.MockStatus,
                detail: $"Mock returned configured failure status {config.MockStatus}.",
                errorCode: "MOCK_FAILURE"
            );
            return (null, failure);
        }

        var fixturePath = ResolveSeedFilePath(service, operation);
        if (fixturePath == null || !File.Exists(fixturePath))
        {
            var missing = _responseFactory.FailureResult(
                context,
                title: "Seed data not found",
                status: StatusCodes.Status500InternalServerError,
                detail: $"Seed data file for '{service}/{operation}' is missing. Expected at: Seeds/{service}/{operation}.json",
                errorCode: "SEED_DATA_NOT_FOUND"
            );
            return (null, missing);
        }

        var json = await File.ReadAllTextAsync(fixturePath, cancellationToken);
        return (json, null);
    }

    private string? ResolveSeedFilePath(string service, string operation)
    {
        var candidates = new List<string>
        {
            // 1. Feature / Service folders under Seeds/
            Path.Combine(_environment.ContentRootPath, "Seeds", service, $"{operation}.json"),
            Path.Combine(_environment.ContentRootPath, "Seeds", operation, $"{operation}.json"),
            Path.Combine(_environment.ContentRootPath, "Seeds", "auth", $"{operation}.json"),
            Path.Combine(_environment.ContentRootPath, "Seeds", "users", $"{operation}.json"),
            // 2. Flat format under Seeds/
            Path.Combine(_environment.ContentRootPath, "Seeds", $"{service}-{operation}.json"),
            Path.Combine(_environment.ContentRootPath, "Seeds", $"{operation}.json"),
            // 3. Fallback compatibility with Mocks/
            Path.Combine(_environment.ContentRootPath, "Mocks", service, $"{operation}.json"),
            Path.Combine(_environment.ContentRootPath, "Mocks", $"{operation}.json")
        };

        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }
}
