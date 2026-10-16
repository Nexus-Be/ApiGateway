namespace Nexus.Gateway;

using Google.Protobuf;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Nexus.Gateway.Dispatching;

public sealed class BackendDispatcher
{
    public const string ActivityName = GatewayDispatcher.ActivityName;
    private readonly IGatewayDispatcher _dispatcher;

    public BackendDispatcher(IGatewayDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public Task<IResult> Execute<T>(
        HttpContext context,
        string service,
        string operation,
        Func<CallOptions, AsyncUnaryCall<T>> call,
        MessageParser<T> parser,
        string? successMessage = null,
        int successStatusCode = StatusCodes.Status200OK) where T : class, IMessage<T> =>
        _dispatcher.DispatchAsync(context, service, operation, call, parser, successMessage, successStatusCode);

    public Task<IResult> ExecutePaged<T>(
        HttpContext context,
        string service,
        string operation,
        int pageIndex,
        int pageSize,
        Func<CallOptions, AsyncUnaryCall<T>> call,
        MessageParser<T> parser,
        int successStatusCode = StatusCodes.Status200OK) where T : class, IMessage<T> =>
        _dispatcher.DispatchPagedAsync(context, service, operation, pageIndex, pageSize, call, parser, successStatusCode);
}
