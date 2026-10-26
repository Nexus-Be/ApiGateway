namespace Nexus.Gateway.Dtos.Log;

public sealed class QueryLogsRequestDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? From { get; set; }
    public string? To { get; set; }
    public string? ServiceName { get; set; }
    public string? EventType { get; set; }
    public string? EntityName { get; set; }
    public string? EntityId { get; set; }
    public string? ActorUserId { get; set; }
    public string? CorrelationId { get; set; }
    public string? LogLevel { get; set; }
}

public sealed class SetLogEventTypeEnabledDto
{
    public bool Enabled { get; set; }
}

public sealed class BatchLogEntryDto
{
    public string? ServiceName { get; set; }
    public string? EventType { get; set; }
    public string? Action { get; set; }
    public string? EntityName { get; set; }
    public string? EntityId { get; set; }
    public string? ActorUserId { get; set; }
    public string? ActorUsername { get; set; }
    public string? ActorRole { get; set; }
    public string? CorrelationId { get; set; }
    public string? TraceId { get; set; }
    public string? Payload { get; set; }
    public string? LogLevel { get; set; }
}

public sealed class BatchLogRequestDto
{
    public List<BatchLogEntryDto> Entries { get; set; } = [];
}
