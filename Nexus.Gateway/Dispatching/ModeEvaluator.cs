namespace Nexus.Gateway.Dispatching;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Nexus.Gateway.Services;
using Nexus.Gateway.Security;

public interface IModeEvaluator
{
    string EvaluateMode(HttpContext context, string service);
    BackendOptions? GetServiceOptions(string service);
}

public sealed class ModeEvaluator : IModeEvaluator
{
    private readonly IOptionsMonitor<GatewayOptions> _options;
    private readonly IGatewayRuntimeManager _runtimeManager;

    public ModeEvaluator(
        IOptionsMonitor<GatewayOptions> options,
        IGatewayRuntimeManager runtimeManager)
    {
        _options = options;
        _runtimeManager = runtimeManager;
    }

    public string EvaluateMode(HttpContext context, string service)
    {
        var config = _options.CurrentValue.GetService(service);
        var configuredMode = config?.Mode ?? "Mock";
        return GatewayRequestResolution.Get(context) is { } resolution
            ? resolution.EffectiveMode(context, service, _options.CurrentValue, _runtimeManager)
            : _runtimeManager.ResolveEffectiveMode(context, service, configuredMode);
    }

    public BackendOptions? GetServiceOptions(string service)
    {
        return _options.CurrentValue.GetService(service);
    }
}
