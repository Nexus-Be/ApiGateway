namespace Nexus.Gateway.Dtos.GatewayControl;

public sealed record AuthModeUpdateRequestDto(string Mode);
public sealed record ServiceModeUpdateRequestDto(string Mode);
public sealed record BulkServiceModeUpdateRequestDto(string? AuthMode, string? GatewayMode, Dictionary<string, string>? Services);

// Compatibility aliases
public sealed record AuthModeUpdateBody(string Mode);
public sealed record ServiceModeUpdateBody(string Mode);
public sealed record BulkServiceModeUpdateBody(string? AuthMode, string? GatewayMode, Dictionary<string, string>? Services);
