using Microsoft.AspNetCore.Http;

namespace Nexus.Gateway.Services;

public sealed record ServiceModeInfo(string Service, string Mode, string Address, int TimeoutSeconds);

public sealed record ServiceModeUpdateBody(string Mode);

public sealed record AuthModeUpdateBody(string Mode); // "Enforce" | "Bypass"

public sealed record BulkServiceModeUpdateBody(
    Dictionary<string, string>? Services,
    string? AuthMode,
    string? GatewayMode = null);

public sealed record GatewayStateResponse(string AuthMode, IReadOnlyDictionary<string, ServiceModeInfo> Services)
{
    // Compatibility field for existing frontend clients. New clients use authMode.
    public string GatewayMode => AuthMode == "Bypass" ? "Bypass" : "Standard";
}

public enum BulkModesUpdateResult
{
    Success,
    InvalidAuthMode,
    InvalidServiceMode
}

public interface IGatewayRuntimeManager
{
    string GetAuthMode(HttpContext? context = null);

    Task<bool> SetAuthModeAsync(string mode, CancellationToken cancellationToken = default);

    IReadOnlyDictionary<string, ServiceModeInfo> GetServiceModes();

    Task<bool> SetServiceModeAsync(string service, string mode, CancellationToken cancellationToken = default);

    Task<bool> SetBulkServiceModesAsync(Dictionary<string, string> updates, CancellationToken cancellationToken = default);

    Task<BulkModesUpdateResult> SetBulkModesAsync(string? authMode, Dictionary<string, string>? services, CancellationToken cancellationToken = default);

    string ResolveEffectiveMode(HttpContext context, string service, string configuredMode);

    bool IsAuthBypassed(HttpContext context);
}
