namespace Nexus.Gateway.Dtos.Audit;

public sealed record AuditLogResponseDto(string Id, string ServiceName, string EventType, string EntityName, string EntityId, string ActorUserId, string Timestamp);
public sealed record AuditEventTypeResponseDto(string Code, string Name, bool Enabled);
