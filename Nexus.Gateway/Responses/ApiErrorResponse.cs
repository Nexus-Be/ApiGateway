using System.Text.Json.Serialization;

namespace Nexus.Gateway.Responses;

public sealed record ApiErrorResponse
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "https://tools.ietf.org/html/rfc9110";

    [JsonPropertyName("title")]
    public string Title { get; init; } = "An error occurred";

    [JsonPropertyName("status")]
    public int Status { get; init; } = 400;

    [JsonPropertyName("errorCode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorCode { get; init; }

    [JsonPropertyName("detail")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Detail { get; init; }

    [JsonPropertyName("errors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, string[]>? Errors { get; init; }

    [JsonPropertyName("traceId")]
    public string TraceId { get; init; } = "";

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; init; } = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
}
