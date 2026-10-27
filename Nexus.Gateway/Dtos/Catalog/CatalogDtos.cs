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
    public string? Reason { get => StatusReason; set => StatusReason = value; }
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

// ============================================================================
// Response DTOs for Gateway Swagger / Documentation Contracts
// ============================================================================

public sealed class CategoryListResponseDto
{
    public List<object>? Categories { get; set; }
}

public sealed class CategoryDetailResponseDto
{
    public string? CategoryId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
}

public sealed class CategoryCreatedResponseDto
{
    public string? CategoryId { get; set; }
    public string? Name { get; set; }
}

public sealed class CategoryUpdatedResponseDto
{
    public string? CategoryId { get; set; }
    public string? Name { get; set; }
}

public sealed class StockInResponseDto
{
    public string? Message { get; set; }
}

public sealed class StockInVariantResponseDto
{
    public string? Message { get; set; }
}

public sealed class ProductListItemDto
{
    public string? ProductId { get; set; }
    public string? Name { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
}

public sealed class ProductDetailResponseDto
{
    public string? ProductId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
}

public sealed class ProductCreatedResponseDto
{
    public string? ProductId { get; set; }
    public string? Name { get; set; }
}

public sealed class ProductUpdatedResponseDto
{
    public string? ProductId { get; set; }
    public string? Message { get; set; }
}

public sealed class ProductImageAddResponseDto
{
    public List<string>? ImageUrls { get; set; }
}

public sealed class ProductImageListResponseDto
{
    public List<object>? Images { get; set; }
}

public sealed class ProductRatingSummaryResponseDto
{
    public double AverageRating { get; set; }
    public int TotalRatings { get; set; }
}

public sealed class ProductRatingResponseDto
{
    public double Rating { get; set; }
}

public sealed class ProductStatusResponseDto
{
    public string? Status { get; set; }
}

public sealed class VariantCreatedResponseDto
{
    public string? VariantId { get; set; }
    public string? SkuCode { get; set; }
}

public sealed class VariantUpdatedResponseDto
{
    public string? VariantId { get; set; }
}

public sealed class VariantDetailResponseDto
{
    public string? VariantId { get; set; }
    public string? SkuCode { get; set; }
}

public sealed class AuctionProductCreatedResponseDto
{
    public string? ProductId { get; set; }
    public string? Name { get; set; }
}

public sealed class AuctionProductDetailResponseDto
{
    public string? ProductId { get; set; }
    public string? Name { get; set; }
}
