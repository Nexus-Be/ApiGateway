using System.Text.Json.Serialization;

namespace Nexus.Gateway.Responses;

public class ApiResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; init; } = true;

    [JsonPropertyName("statusCode")]
    public int StatusCode { get; init; } = 200;

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; init; }

    [JsonPropertyName("data")]
    public T? Data { get; init; }

    [JsonPropertyName("traceId")]
    public string TraceId { get; init; } = "";

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; init; } = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
}
