using System.Text.Json.Serialization;

namespace Nexus.Gateway.Responses;

public sealed record PaginationMetadata
{
    [JsonPropertyName("pageIndex")]
    public int PageIndex { get; init; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; init; }

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; init; }

    [JsonPropertyName("totalPages")]
    public int TotalPages { get; init; }

    [JsonPropertyName("hasPreviousPage")]
    public bool HasPreviousPage { get; init; }

    [JsonPropertyName("hasNextPage")]
    public bool HasNextPage { get; init; }

    public static PaginationMetadata Create(int pageIndex, int pageSize, int totalCount)
    {
        var safePage = pageIndex <= 0 ? 1 : pageIndex;
        var safeSize = pageSize <= 0 ? 10 : pageSize;
        var totalPages = totalCount <= 0 ? 0 : (int)Math.Ceiling((double)totalCount / safeSize);

        return new PaginationMetadata
        {
            PageIndex = safePage,
            PageSize = safeSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            HasPreviousPage = safePage > 1,
            HasNextPage = safePage < totalPages
        };
    }
}
