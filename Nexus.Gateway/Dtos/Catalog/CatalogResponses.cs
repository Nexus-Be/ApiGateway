namespace Nexus.Gateway.Dtos.Catalog;

public sealed record CategoryResponseDto(string Id, string Name, string? ParentId);
public sealed record ProductResponseDto(string Id, string Name, string? Description, string? CategoryId, string Price, string Currency, int Available, string Slug);
public sealed record InventoryResponseDto(string ProductId, int Available);
