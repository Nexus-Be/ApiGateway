namespace Nexus.Gateway.Dtos.Catalog;

using System;

public sealed record CreateCategoryDto(
    string Name,
    string? Description,
    Guid? ParentId,
    short Level,
    int SortOrder);

public sealed record UpdateCategoryDto(
    string Name,
    string? Description,
    short Level,
    int SortOrder,
    bool IsDisabled);

public sealed record CreateProductDto(
    Guid SellerId,
    Guid CategoryId,
    string Name,
    string? Description,
    string? Brand,
    decimal Price,
    string? Currency);

public sealed record UpdateProductDto(
    string Name,
    string? Description,
    string? Brand,
    decimal Price,
    string Status);

public sealed record AdjustInventoryDto(int QuantityChange);
