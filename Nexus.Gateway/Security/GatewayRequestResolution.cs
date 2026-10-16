using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Nexus.Gateway.MockEngine;
using Nexus.Gateway.Services;

namespace Nexus.Gateway.Security;

/// <summary>
/// A per-request view of the routed endpoint, declarative match and effective service modes.
/// Resolved after routing and before authentication; never shared between HTTP requests.
/// </summary>
public sealed class GatewayRequestResolution
{
    private const string ItemKey = "Nexus.Gateway.RequestResolution";
    private readonly Dictionary<string, string> _modes = new(StringComparer.OrdinalIgnoreCase);

    private GatewayRequestResolution(string service, MockRouteMatch match)
    {
        Service = service;
        MockMatch = match;
    }

    public string Service { get; }
    public MockRouteMatch MockMatch { get; }

    public static GatewayRequestResolution? Get(HttpContext context) =>
        context.Items.TryGetValue(ItemKey, out var value) ? value as GatewayRequestResolution : null;

    public static GatewayRequestResolution Resolve(
        HttpContext context, IMockEngine mockEngine, IOptionsMonitor<GatewayOptions> options,
        IGatewayRuntimeManager runtimeManager)
    {
        if (Get(context) is { } existing) return existing;

        var match = mockEngine.Match(context.Request.Method, context.Request.Path);
        // Preserve routed endpoint metadata for auth/rate-limits, even when a mock definition
        // also matches. MockEngine can use the definition's own service when serving it.
        var service = context.GetEndpoint()?.Metadata.GetMetadata<BackendTag>()?.Name
            ?? context.GetEndpoint()?.Metadata.GetMetadata<BackendTagAttribute>()?.Name;
        if (string.IsNullOrEmpty(service))
            service = match.IsMatch && !string.IsNullOrEmpty(match.Definition?.Service)
                ? match.Definition.Service
                : GatewaySecurityHelper.InferServiceFromUrl(context.Request.Path);

        var resolution = new GatewayRequestResolution(service.ToLowerInvariant(), match);
        resolution.EffectiveMode(context, resolution.Service, options.CurrentValue, runtimeManager);
        if (match.IsMatch && match.Definition is { } definition &&
            !string.Equals(resolution.Service, definition.Service, StringComparison.OrdinalIgnoreCase))
            resolution.EffectiveMode(context, definition.Service, options.CurrentValue, runtimeManager);

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
