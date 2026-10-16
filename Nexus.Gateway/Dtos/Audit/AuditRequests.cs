namespace Nexus.Gateway.Dtos.Audit;

public sealed record AuditEventTypeToggleRequestDto(bool Enabled);

// Compatibility aliases
public sealed record AuditEventTypeToggleBody(bool Enabled);
