using System.Text.Json.Serialization;

namespace Nexus.Gateway.Responses;

public sealed record PagedData<T>
{
    [JsonPropertyName("items")]
    public IReadOnlyList<T> Items { get; init; } = [];

    [JsonPropertyName("pagination")]
    public PaginationMetadata Pagination { get; init; } = null!;
}
