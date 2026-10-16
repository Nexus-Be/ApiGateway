namespace Nexus.Gateway.Dtos.Catalog;

public sealed record CategoryRequestDto(string Name, string? ParentId);
public sealed record ProductRequestDto(string Name, string? Description, string? CategoryId, string Price, string? Currency, int Available, string? Slug);
public sealed record AdjustInventoryRequestDto(int Available, string? Reason);

// Compatibility aliases
public sealed record CategoryBody(string Name, string? ParentId);
public sealed record ProductBody(string Name, string? Description, string? CategoryId, string Price, string? Currency, int Available, string? Slug);
public sealed record AdjustInventoryBody(int Available, string? Reason);
