using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nexus.Gateway.Services;

public sealed class GatewayRuntimeManager : IGatewayRuntimeManager
{
    private readonly IOptionsMonitor<GatewayOptions> _options;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<GatewayRuntimeManager> _logger;
    private readonly ConcurrentDictionary<string, string> _memoryOverrides = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _runtimeConfigPath;
    private volatile string? _memoryGatewayMode;

    public GatewayRuntimeManager(
        IOptionsMonitor<GatewayOptions> options,
        IWebHostEnvironment environment,
        ILogger<GatewayRuntimeManager> logger)
    {
        _options = options;
        _environment = environment;
        _logger = logger;
        var runtimeJson = Path.Combine(_environment.ContentRootPath, "gateway.runtime.json");
        var devSettings = Path.Combine(_environment.ContentRootPath, $"appsettings.{_environment.EnvironmentName}.json");
        _runtimeConfigPath = File.Exists(runtimeJson) ? runtimeJson : devSettings;
    }

    public string GetAuthMode(HttpContext? context = null)
    {
        if (context != null && _environment.IsDevelopment() && _options.CurrentValue.AllowClientModeOverrideInDevelopment)
        {
            if (context.Request.Headers.TryGetValue("X-Gateway-Auth", out var authHeader) &&
                IsValidAuthMode(authHeader.ToString(), out var parsedHeaderMode))
            {
                return parsedHeaderMode;
            }

            if (context.Request.Headers.TryGetValue("X-Gateway-Mode", out var modeHeader) &&
                IsValidAuthMode(modeHeader.ToString(), out var parsedGatewayMode))
            {
                return parsedGatewayMode;
            }
        }

        var configuredMode = _options.CurrentValue.AuthMode ?? _options.CurrentValue.GatewayMode ?? "Enforce";
        return _memoryGatewayMode ?? NormalizeConfiguredAuthMode(configuredMode);
    }

    public bool IsAuthBypassed(HttpContext context)
    {
        return _environment.IsDevelopment() &&
               string.Equals(GetAuthMode(context), "Bypass", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> SetAuthModeAsync(string mode, CancellationToken cancellationToken = default)
    {
        if (!IsValidAuthMode(mode, out var normalizedMode) ||
            (normalizedMode == "Bypass" && !_environment.IsDevelopment()))
        {
            return false;
        }

        _memoryGatewayMode = normalizedMode;
        await PersistGatewayModeToFileAsync(normalizedMode, cancellationToken);
        return true;
    }

    public IReadOnlyDictionary<string, ServiceModeInfo> GetServiceModes()
    {
        var result = new Dictionary<string, ServiceModeInfo>(StringComparer.OrdinalIgnoreCase);
        var currentOptions = _options.CurrentValue;

        foreach (var service in GatewayOptions.CanonicalServices)
        {
            var config = currentOptions.GetService(service);
            var mode = _memoryOverrides.TryGetValue(service, out var memMode)
                ? memMode
                : config?.Mode ?? "Mock";

            result[service] = new ServiceModeInfo(
                Service: service,
                Mode: mode,
                Address: config?.Address ?? "",
                TimeoutSeconds: config?.TimeoutSeconds ?? 5
            );
        }

        return result;
    }

    public string ResolveEffectiveMode(HttpContext context, string service, string configuredMode)
    {
        var currentOptions = _options.CurrentValue;

        if (_environment.IsDevelopment() && currentOptions.AllowClientModeOverrideInDevelopment)
        {
            // 1. Specific service header: e.g. X-Service-Mode-Catalog: Real or X-Service-Mode: catalog=Real;auction=Mock
            if (context.Request.Headers.TryGetValue($"X-Service-Mode-{service}", out var specificHeader) &&
                IsValidMode(specificHeader.ToString(), out var parsedSpecific))
            {
                return parsedSpecific;
            }

            if (context.Request.Headers.TryGetValue("X-Service-Modes", out var mapHeader))
            {
                var pairs = mapHeader.ToString().Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var pair in pairs)
                {
                    var kv = pair.Split(new[] { '=', ':' }, StringSplitOptions.TrimEntries);
                    if (kv.Length == 2 && kv[0].Equals(service, StringComparison.OrdinalIgnoreCase) && IsValidMode(kv[1], out var parsedMap))
                    {
                        return parsedMap;
                    }
                }
            }

            // 2. Global request header: X-Mock-Mode or X-Gateway-Mode-Override
            if (context.Request.Headers.TryGetValue("X-Mock-Mode", out var globalMockHeader) &&
                IsValidMode(globalMockHeader.ToString(), out var parsedGlobal))
            {
                return parsedGlobal;
            }

            if (context.Request.Headers.TryGetValue("X-Gateway-Mode-Override", out var globalOverrideHeader) &&
                IsValidMode(globalOverrideHeader.ToString(), out var parsedOverride))
            {
                return parsedOverride;
            }
        }

        // 3. Memory override
        if (_memoryOverrides.TryGetValue(service, out var memoryMode))
        {
            return memoryMode;
        }

        return configuredMode;
    }

    public async Task<bool> SetServiceModeAsync(string service, string mode, CancellationToken cancellationToken = default)
    {
        if (!GatewayOptions.CanonicalServices.Contains(service, StringComparer.OrdinalIgnoreCase) &&
            !service.Equals("user", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!IsValidMode(mode, out var normalizedMode) ||
            (normalizedMode == "Mock" && !_environment.IsDevelopment()))
        {
            return false;
        }

        _memoryOverrides[service] = normalizedMode;
        if (service.Equals("user", StringComparison.OrdinalIgnoreCase))
            _memoryOverrides["users"] = normalizedMode;
        else if (service.Equals("users", StringComparison.OrdinalIgnoreCase))
            _memoryOverrides["user"] = normalizedMode;

        await PersistToFileAsync(new Dictionary<string, string> { [service] = normalizedMode }, cancellationToken);
        return true;
    }

    public async Task<bool> SetBulkServiceModesAsync(Dictionary<string, string> updates, CancellationToken cancellationToken = default)
    {
        var validated = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (svc, mode) in updates)
        {
            var knownService = GatewayOptions.CanonicalServices.Contains(svc, StringComparer.OrdinalIgnoreCase) ||
                               svc.Equals("user", StringComparison.OrdinalIgnoreCase);
            if (!knownService || !IsValidMode(mode, out var normalizedMode) ||
                (normalizedMode == "Mock" && !_environment.IsDevelopment()))
                return false;

            validated[svc] = normalizedMode;
        }

        if (validated.Count == 0) return false;

        foreach (var (svc, normalizedMode) in validated)
        {
            _memoryOverrides[svc] = normalizedMode;
            if (svc.Equals("user", StringComparison.OrdinalIgnoreCase))
                _memoryOverrides["users"] = normalizedMode;
            else if (svc.Equals("users", StringComparison.OrdinalIgnoreCase))
                _memoryOverrides["user"] = normalizedMode;
        }

        await PersistToFileAsync(validated, cancellationToken);
        return true;
    }

    public async Task<BulkModesUpdateResult> SetBulkModesAsync(
        string? authMode, Dictionary<string, string>? services, CancellationToken cancellationToken = default)
    {
        string? normalizedAuthMode = null;
        if (!string.IsNullOrEmpty(authMode))
        {
            if (!IsValidAuthMode(authMode, out var parsedAuthMode) ||
                (parsedAuthMode == "Bypass" && !_environment.IsDevelopment()))
                return BulkModesUpdateResult.InvalidAuthMode;

            normalizedAuthMode = parsedAuthMode;
        }

        var validated = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (services != null)
        {
            foreach (var (service, mode) in services)
            {
                var knownService = GatewayOptions.CanonicalServices.Contains(service, StringComparer.OrdinalIgnoreCase) ||
                                   service.Equals("user", StringComparison.OrdinalIgnoreCase);
                if (!knownService || !IsValidMode(mode, out var normalizedMode) ||
                    (normalizedMode == "Mock" && !_environment.IsDevelopment()))
                    return BulkModesUpdateResult.InvalidServiceMode;

                validated[service] = normalizedMode;
            }

            if (validated.Count == 0) return BulkModesUpdateResult.InvalidServiceMode;
        }

        // Persistence is best-effort, as for the individual setters. Write both groups in
        // one file update so a failed write cannot leave only one group persisted.
        if (normalizedAuthMode != null || validated.Count > 0)
            await PersistToFileAsync(validated, cancellationToken, normalizedAuthMode);

        if (normalizedAuthMode != null) _memoryGatewayMode = normalizedAuthMode;
        foreach (var (service, mode) in validated)
        {
            _memoryOverrides[service] = mode;
            if (service.Equals("user", StringComparison.OrdinalIgnoreCase))
                _memoryOverrides["users"] = mode;
            else if (service.Equals("users", StringComparison.OrdinalIgnoreCase))
                _memoryOverrides["user"] = mode;
        }

        return BulkModesUpdateResult.Success;
    }

    private static bool IsValidMode(string? input, out string normalized)
    {
        if (string.Equals(input, "Mock", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "Mock";
            return true;
        }
        if (string.Equals(input, "Real", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "Real";
            return true;
        }
        normalized = "";
        return false;
    }

    private static bool IsValidAuthMode(string? input, out string normalized)
    {
        if (string.Equals(input, "Bypass", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(input, "Mock", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(input, "MockBypass", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "Bypass";
            return true;
        }
        if (string.Equals(input, "Enforce", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(input, "Standard", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(input, "Normal", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(input, "Strict", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "Standard";
            return true;
        }
        normalized = "";
        return false;
    }

    private static string NormalizeConfiguredAuthMode(string configuredMode) =>
        IsValidAuthMode(configuredMode, out var normalized) ? normalized : "Standard";

    private async Task PersistGatewayModeToFileAsync(string mode, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(_runtimeConfigPath)) return;

            var jsonContent = await File.ReadAllTextAsync(_runtimeConfigPath, cancellationToken);
            var rootNode = JsonNode.Parse(jsonContent);
            if (rootNode is not JsonObject rootObj) return;

            var gatewayNode = rootObj["Gateway"] as JsonObject;
            if (gatewayNode == null) return;

            gatewayNode["AuthMode"] = mode;
            gatewayNode.Remove("GatewayMode");

            var updatedJson = rootNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_runtimeConfigPath, updatedJson, cancellationToken);
            _logger.LogInformation("Successfully persisted GatewayMode '{Mode}' to {ConfigPath}", mode, _runtimeConfigPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist GatewayMode to {ConfigPath}", _runtimeConfigPath);
        }
    }

    private async Task PersistToFileAsync(Dictionary<string, string> updates, CancellationToken cancellationToken, string? authMode = null)
    {
        try
        {
            if (!File.Exists(_runtimeConfigPath)) return;

            var jsonContent = await File.ReadAllTextAsync(_runtimeConfigPath, cancellationToken);
            var rootNode = JsonNode.Parse(jsonContent);
            if (rootNode is not JsonObject rootObj) return;

            var gatewayNode = rootObj["Gateway"] as JsonObject;
            if (gatewayNode == null) return;

            var servicesNode = gatewayNode["Services"] as JsonObject;
            if (updates.Count > 0 && servicesNode == null) return;

            if (authMode != null)
            {
                gatewayNode["AuthMode"] = authMode;
                gatewayNode.Remove("GatewayMode");
            }

            foreach (var (svc, mode) in updates)
            {
                var targetKey = svc;
                if (!servicesNode!.ContainsKey(targetKey))
                {
                    if (svc.Equals("user", StringComparison.OrdinalIgnoreCase) && servicesNode!.ContainsKey("users"))
                        targetKey = "users";
                    else if (svc.Equals("users", StringComparison.OrdinalIgnoreCase) && servicesNode!.ContainsKey("user"))
                        targetKey = "user";
                }

                if (servicesNode!.TryGetPropertyValue(targetKey, out var svcNode) && svcNode is JsonObject svcObj)
                {
                    svcObj["Mode"] = mode;
                }
            }

            var updatedJson = rootNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_runtimeConfigPath, updatedJson, cancellationToken);
            _logger.LogInformation("Successfully persisted service mode updates to {ConfigPath}", _runtimeConfigPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist service mode updates to {ConfigPath}. Memory override remains active.", _runtimeConfigPath);
        }
    }
}
