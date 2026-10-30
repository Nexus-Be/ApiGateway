using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Nexus.Gateway.Services;

namespace Nexus.Gateway.Security;

/// <summary>
/// A per-request view of the routed endpoint and its effective service modes.
/// Resolved after routing and before authentication; never shared between HTTP requests.
/// </summary>
public sealed class GatewayRequestResolution
{
    private const string ItemKey = "Nexus.Gateway.RequestResolution";
    private readonly Dictionary<string, string> _modes = new(StringComparer.OrdinalIgnoreCase);

    private GatewayRequestResolution(string service)
    {
        Service = service;
    }

    public string Service { get; }

    public static GatewayRequestResolution? Get(HttpContext context) =>
        context.Items.TryGetValue(ItemKey, out var value) ? value as GatewayRequestResolution : null;

    public static GatewayRequestResolution Resolve(
        HttpContext context, IOptionsMonitor<GatewayOptions> options,
        IGatewayRuntimeManager runtimeManager)
    {
        if (Get(context) is { } existing) return existing;

        var service = context.GetEndpoint()?.Metadata.GetMetadata<BackendTag>()?.Name
            ?? context.GetEndpoint()?.Metadata.GetMetadata<BackendTagAttribute>()?.Name;
        if (string.IsNullOrEmpty(service))
            service = GatewaySecurityHelper.InferServiceFromUrl(context.Request.Path);

        var resolution = new GatewayRequestResolution(service.ToLowerInvariant());
        resolution.EffectiveMode(context, resolution.Service, options.CurrentValue, runtimeManager);

        context.Items[ItemKey] = resolution;
        return resolution;
    }

    public string EffectiveMode(
        HttpContext context, string service, GatewayOptions options, IGatewayRuntimeManager runtimeManager)
    {
        if (_modes.TryGetValue(service, out var cached)) return cached;
        var configured = options.GetService(service)?.Mode ?? "Mock";
        var mode = runtimeManager.ResolveEffectiveMode(context, service, configured);
        _modes[service] = mode;
        return mode;
    }
}
