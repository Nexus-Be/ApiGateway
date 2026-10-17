namespace Nexus.Gateway.Dtos.Log;

public sealed record LogEntryResponseDto(string Id, string ServiceName, string EventType, string EntityName, string EntityId, string ActorUserId, string Timestamp, string LogLevel);
public sealed record LogEventTypeResponseDto(string Code, string Name, bool Enabled);
