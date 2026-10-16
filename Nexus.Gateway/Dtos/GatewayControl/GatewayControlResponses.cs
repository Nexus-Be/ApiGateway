namespace Nexus.Gateway.Dtos.GatewayControl;

using Nexus.Gateway.Services;

public sealed record GatewayStateResponseDto(string GatewayMode, IReadOnlyDictionary<string, ServiceModeInfo> Services);

// Compatibility alias
public sealed record GatewayStateResponse(string GatewayMode, IReadOnlyDictionary<string, ServiceModeInfo> Services);
