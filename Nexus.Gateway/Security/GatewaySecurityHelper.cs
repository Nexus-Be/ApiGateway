using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Nexus.Gateway.MockEngine;
using Nexus.Gateway.Services;

namespace Nexus.Gateway.Security;

public static class GatewaySecurityHelper
{
    public static string DetectServiceForRequest(HttpContext context, IMockEngine? mockEngine = null)
    {
        // 1. From Endpoint Metadata BackendTag
        var backendTag = context.GetEndpoint()?.Metadata.GetMetadata<BackendTag>()?.Name
            ?? context.GetEndpoint()?.Metadata.GetMetadata<BackendTagAttribute>()?.Name;
        if (!string.IsNullOrEmpty(backendTag))
        {
            return backendTag.ToLowerInvariant();
        }

        // 2. From MockEngine if matched
        if (mockEngine != null)
        {
            var match = mockEngine.Match(context.Request.Method, context.Request.Path);
            if (match.IsMatch && !string.IsNullOrEmpty(match.Definition?.Service))
            {
                return match.Definition.Service.ToLowerInvariant();
            }
        }

        // 3. From URL Path conventions
        var path = context.Request.Path.Value ?? "";
        return InferServiceFromUrl(path);
    }

    public static string InferServiceFromUrl(string path)
    {
        var clean = path.Trim('/').ToLowerInvariant();
        var segments = clean.Split('/', StringSplitOptions.RemoveEmptyEntries);

        var offset = 0;
        if (segments.Length >= 1 && segments[0] == "gateway")
        {
            offset = 1;
        }

        if (segments.Length >= offset + 3 && segments[offset] == "api" && segments[offset + 1] == "v1")
        {
            var section = segments[offset + 2];
            if (section is "seller")
            {
                if (segments.Length >= offset + 4 && segments[offset + 3] == "orders") return "commerce";
                return "catalog";
            }
            if (section is "admin")
            {
                if (segments.Length >= offset + 4 && segments[offset + 3] == "orders") return "commerce";
                return "users";
            }
            return GatewayOptions.ResolveCanonicalName(section);
        }

        return "default";
    }

    public static bool IsRequestInMockMode(
        HttpContext context,
        GatewayOptions options,
        IGatewayRuntimeManager runtimeManager,
        IMockEngine? mockEngine = null)
    {
        var service = DetectServiceForRequest(context, mockEngine);
        var config = options.GetService(service);
        var configuredMode = config?.Mode ?? "Mock";
        var effectiveMode = runtimeManager.ResolveEffectiveMode(context, service, configuredMode);

        return string.Equals(effectiveMode, "Mock", StringComparison.OrdinalIgnoreCase);
    }

    public static string ResolveRequiredPolicy(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint == null) return "public";

        var authorizeData = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
        foreach (var data in authorizeData)
        {
            if (!string.IsNullOrEmpty(data.Policy))
            {
                return data.Policy.ToLowerInvariant();
            }
            if (!string.IsNullOrEmpty(data.Roles))
            {
                return data.Roles.ToLowerInvariant();
            }
        }

        return "public";
    }
}
