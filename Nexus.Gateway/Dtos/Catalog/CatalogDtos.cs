namespace Nexus.Gateway.Dtos.Catalog;

using System.Text.Json;
using Microsoft.AspNetCore.Http;

// HTTP request shapes of the F2 catalog contract (products, categories, inventory).
// camelCase on the wire — System.Text.Json binds case-insensitively by default.

public sealed class AttributeDto
{
    public string? AttrName { get; set; }
    public string? AttrValue { get; set; }
}

public sealed class CreateVariantDto
{
    public string? SkuCode { get; set; }
    public string? Name { get; set; }
    public decimal? Price { get; set; }
    public string? Currency { get; set; }
    public bool IsActive { get; set; }
    public List<AttributeDto>? Attributes { get; set; }
}

public sealed class CreateProductDto
{
    public Guid? CategoryId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Brand { get; set; }
    public JsonElement? Specifications { get; set; }
    public List<CreateVariantDto>? Variants { get; set; }
}

public sealed class UpdateProductDto
{
    public Guid? CategoryId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Brand { get; set; }
    public JsonElement? Specifications { get; set; }
}

public sealed class UpdateProductStatusDto
{
    public string? Status { get; set; }
    public string? StatusReason { get; set; }
}

public sealed class UpdateVariantDto
{
    public string? Name { get; set; }
    public decimal? Price { get; set; }
    public string? Currency { get; set; }
    public List<AttributeDto>? Attributes { get; set; }
}

public sealed class StockInItemDto
{
    public Guid? SkuId { get; set; }
    public int Quantity { get; set; }
}

public sealed class StockInDto
{
    public List<StockInItemDto>? Items { get; set; }
}

public sealed class StockInVariantDto
{
    public int Quantity { get; set; }
}

public sealed class CreateCategoryDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public Guid? ParentId { get; set; }
    public int SortOrder { get; set; }
}

public sealed class UpdateCategoryDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public Guid? ParentId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class AuctionVariantDto
{
    public string? SkuCode { get; set; }
    public string? Name { get; set; }
    public List<AttributeDto>? Attributes { get; set; }
}

public sealed class CreateAuctionProductDto
{
    public Guid? CategoryId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Brand { get; set; }
    public JsonElement? Specifications { get; set; }
    public AuctionVariantDto? Variant { get; set; }
}

// multipart/form-data body of POST /api/v1/product/{productId}pics
public sealed class AddProductImagesForm
{
    public List<IFormFile>? Files { get; set; }
    public Guid? VariantId { get; set; }
    public string? AltText { get; set; }
    public bool IsActive { get; set; }
}
