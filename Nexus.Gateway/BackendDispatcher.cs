namespace Nexus.Gateway;

using System.Threading.Tasks;
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

    public Task<IResult> ForwardAsync(HttpContext context, RestForwardRequest request) =>
        _dispatcher.DispatchRestAsync(context, request);

    public Task<IResult> ForwardPagedAsync(HttpContext context, RestPagedForwardRequest request) =>
        _dispatcher.DispatchRestPagedAsync(context, request);

    public Task<IResult> ForwardNoContentAsync(HttpContext context, RestForwardRequest request) =>
        _dispatcher.DispatchRestNoContentAsync(context, request);
}
