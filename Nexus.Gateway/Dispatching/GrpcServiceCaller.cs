namespace Nexus.Gateway.Dispatching;

using System.Text.Json;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Nexus.Gateway.Responses;

public interface IGrpcServiceCaller
{
    Task<JsonElement> CallAsync<T>(
        HttpContext context,
        Func<CallOptions, AsyncUnaryCall<T>> call,
        BackendOptions config,
        CancellationToken cancellationToken) where T : class, IMessage<T>;

    Task<string> CallPagedJsonAsync<T>(
        HttpContext context,
        Func<CallOptions, AsyncUnaryCall<T>> call,
        BackendOptions config,
        CancellationToken cancellationToken) where T : class, IMessage<T>;
}

public sealed class GrpcServiceCaller : IGrpcServiceCaller
{
    public async Task<JsonElement> CallAsync<T>(
        HttpContext context,
        Func<CallOptions, AsyncUnaryCall<T>> call,
        BackendOptions config,
        CancellationToken cancellationToken) where T : class, IMessage<T>
    {
        var callOptions = BuildCallOptions(context, config, cancellationToken);
        using var unaryCall = call(callOptions);
        var response = await unaryCall.ResponseAsync;
        var realFormattedJson = JsonFormatter.Default.Format(response);
        using var realDoc = JsonDocument.Parse(realFormattedJson);
        return realDoc.RootElement.Clone();
    }

    public async Task<string> CallPagedJsonAsync<T>(
        HttpContext context,
        Func<CallOptions, AsyncUnaryCall<T>> call,
        BackendOptions config,
        CancellationToken cancellationToken) where T : class, IMessage<T>
    {
        var callOptions = BuildCallOptions(context, config, cancellationToken);
        using var unaryCall = call(callOptions);
        var response = await unaryCall.ResponseAsync;
        return JsonFormatter.Default.Format(response);
    }

    private static CallOptions BuildCallOptions(HttpContext context, BackendOptions config, CancellationToken cancellationToken)
    {
        var headers = new Metadata();

        var authorization = context.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(authorization))
        {
            headers.Add("authorization", authorization);
        }

        if (context.Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKey))
        {
            headers.Add("idempotency-key", idempotencyKey.ToString());
        }

        var traceId = ApiResponseFactory.ResolveTraceId(context);
        headers.Add("x-trace-id", traceId);

        // Propagate authenticated user identity to downstream microservices
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirst("sub")?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                headers.Add("x-user-id", userId);
            }

            var roles = context.User.FindAll("role").Select(r => r.Value).ToArray();
            if (roles.Length > 0)
            {
                headers.Add("x-user-roles", string.Join(",", roles));
            }

            var email = context.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                ?? context.User.FindFirst("email")?.Value;
            if (!string.IsNullOrEmpty(email))
            {
                headers.Add("x-user-email", email);
            }
        }

        return new CallOptions(
            headers,
            deadline: DateTime.UtcNow.AddSeconds(config.TimeoutSeconds),
            cancellationToken: cancellationToken
        );
    }
}
