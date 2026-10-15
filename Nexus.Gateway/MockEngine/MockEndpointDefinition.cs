using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nexus.Gateway.MockEngine;

public sealed class MockEndpointDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("service")]
    public string Service { get; set; } = "";

    [JsonPropertyName("method")]
    public string Method { get; set; } = "GET";

    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("policy")]
    public string Policy { get; set; } = "public"; // public, authenticated, seller, admin, staff

    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; } = 200;

    [JsonPropertyName("responseType")]
    public string ResponseType { get; set; } = "single"; // single, paged, raw, error

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("request")]
    public JsonElement? Request { get; set; }

    [JsonPropertyName("requestFile")]
    public string? RequestFile { get; set; }

    [JsonPropertyName("response")]
    public JsonElement? Response { get; set; }

    [JsonPropertyName("responseFile")]
    public string? ResponseFile { get; set; }

    [JsonIgnore]
    public string SourceFilePath { get; set; } = "";

    public string NormalizedMethod => Method.ToUpperInvariant().Trim();
    public string NormalizedPath => Path.StartsWith('/') ? Path.Trim() : "/" + Path.Trim();
}
