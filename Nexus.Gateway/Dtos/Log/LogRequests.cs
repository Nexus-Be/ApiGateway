namespace Nexus.Gateway.Dtos.Log;

public sealed record LogEventTypeToggleRequestDto(bool Enabled);

// Compatibility alias
public sealed record LogEventTypeToggleBody(bool Enabled);
