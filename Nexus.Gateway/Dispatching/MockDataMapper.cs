namespace Nexus.Gateway.Dispatching;

using System.Text.Json;
using Google.Protobuf;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Nexus.Gateway.Responses;

public interface IMockDataMapper
{
    Task<(JsonElement? Data, IResult? Failure)> MapMockAsync<T>(
        HttpContext context,
        string service,
        string operation,
        BackendOptions config,
        MessageParser<T> parser,
        CancellationToken cancellationToken) where T : class, IMessage<T>;

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

    public async Task<(JsonElement? Data, IResult? Failure)> MapMockAsync<T>(
        HttpContext context,
        string service,
        string operation,
        BackendOptions config,
        MessageParser<T> parser,
        CancellationToken cancellationToken) where T : class, IMessage<T>
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
                detail: $"Seed data file for '{service}/{operation}' is missing.",
                errorCode: "SEED_DATA_NOT_FOUND"
            );
            return (null, missing);
        }

        var json = await File.ReadAllTextAsync(fixturePath, cancellationToken);
        var protoMessage = parser.ParseJson(json);
        var formattedJson = JsonFormatter.Default.Format(protoMessage);
        using var doc = JsonDocument.Parse(formattedJson);
        var dataElement = doc.RootElement.Clone();

        return (dataElement, null);
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
                detail: $"Seed data file for '{service}/{operation}' is missing.",
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
            Path.Combine(_environment.ContentRootPath, "Seeds", service, $"{operation}.json"),
            Path.Combine(_environment.ContentRootPath, "Mocks", service, $"{operation}.json")
        };

        if (service.Equals("user", StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add(Path.Combine(_environment.ContentRootPath, "Seeds", "users", $"{operation}.json"));
            candidates.Add(Path.Combine(_environment.ContentRootPath, "Mocks", "users", $"{operation}.json"));
        }
        else if (service.Equals("users", StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add(Path.Combine(_environment.ContentRootPath, "Seeds", "user", $"{operation}.json"));
            candidates.Add(Path.Combine(_environment.ContentRootPath, "Mocks", "user", $"{operation}.json"));
        }

        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }
}
