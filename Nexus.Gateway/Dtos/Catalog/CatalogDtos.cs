namespace Nexus.Gateway.Dtos.Catalog;

using System.Text.Json;
using Microsoft.AspNetCore.Http;

// ============================================================
// CATALOG REQUEST DTOs — aligned with flow_mock_excel.xlsx F2
// ============================================================

// ---------- PRODUCT REQUESTS ----------

/// <summary>CAT-01: POST /api/v1/products — tạo sản phẩm mới kèm biến thể và danh mục</summary>
public sealed class CreateProductDto
{
    public Guid? CategoryId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Brand { get; set; }
    public JsonElement? Specifications { get; set; }
    public List<CreateVariantDto>? Variants { get; set; }
}

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
    public string? Currency { get; set; } // default: "VND"
    public bool IsActive { get; set; }
    public List<AttributeDto>? Attributes { get; set; }
}

/// <summary>CAT-02: PATCH /api/v1/product/{productId} — cập nhật thông tin sản phẩm</summary>
public sealed class UpdateProductDto
{
    public Guid? CategoryId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Brand { get; set; }
    public JsonElement? Specifications { get; set; }
}

/// <summary>CAT-12: POST /api/v1/product/{productId}pics — thêm ảnh sản phẩm (multipart)</summary>
public sealed class AddProductImagesForm
{
    public List<IFormFile>? Files { get; set; }
    public Guid? VariantId { get; set; }
    public string? AltText { get; set; }
    public bool IsActive { get; set; }
}

// ---------- VARIANT REQUESTS ----------

/// <summary>CAT-07: POST /api/v1/product/{productId}/variants — thêm biến thể</summary>
public sealed class CreateProductVariantDto
{
    public string? SkuCode { get; set; }
    public string? Name { get; set; }
    public decimal? Price { get; set; }
    public string? Currency { get; set; }
    public bool IsActive { get; set; }
    public List<AttributeDto>? Attributes { get; set; }
}

/// <summary>CAT-08: PATCH /api/v1/product/{productId}/variants/{variantId} — cập nhật biến thể</summary>
public sealed class UpdateVariantDto
{
    public string? Name { get; set; }
    public decimal? Price { get; set; }
    public string? Currency { get; set; }
    public List<AttributeDto>? Attributes { get; set; }
}

// ---------- STOCK REQUESTS ----------

/// <summary>CAT-09/CAT-11: POST /api/v1/product/{productId}/stock — nhập kho</summary>
public sealed class StockInDto
{
    public List<StockInItemDto>? Items { get; set; }
}

public sealed class StockInItemDto
{
    public Guid? SkuId { get; set; }
    public int Quantity { get; set; }
}

/// <summary>Stock in for a single variant</summary>
public sealed class StockInVariantDto
{
    public int Quantity { get; set; }
}

// ---------- CATEGORY REQUESTS ----------

/// <summary>CAT-31: POST /api/v1/categories — tạo danh mục</summary>
public sealed class CreateCategoryDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public Guid? ParentId { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>CAT-32: PUT /api/v1/categories/{categoryId} — cập nhật danh mục</summary>
public sealed class UpdateCategoryDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public Guid? ParentId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

// ---------- AUCTION PRODUCT REQUESTS ----------

/// <summary>Create auction product (F3)</summary>
public sealed class CreateAuctionProductDto
{
    public Guid? CategoryId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Brand { get; set; }
    public JsonElement? Specifications { get; set; }
    public AuctionVariantDto? Variant { get; set; }
}

public sealed class AuctionVariantDto
{
    public string? SkuCode { get; set; }
    public string? Name { get; set; }
    public List<AttributeDto>? Attributes { get; set; }
}

// ============================================================
// CATALOG RESPONSE DTOs — aligned with flow_mock_excel.xlsx F2
// ============================================================

// ---------- PRODUCT LIST (CAT-06) ----------

/// <summary>GET /api/v1/products — list item</summary>
public sealed class ProductListItemDto
{
    public string? ProductId { get; set; }
    public string? Name { get; set; }
    public string? Brand { get; set; }
    public string? SaleType { get; set; }       // REGULAR | AUCTION
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string? PrimaryImageUrl { get; set; }
    public decimal? AvgRating { get; set; }
    public string? Status { get; set; }          // DRAFT | ACTIVE | INACTIVE
    public string? AuctionStatus { get; set; }   // DRAFT|SCHEDULED|RUNNING|ENDED|SETTLED|FAILED|CANCELLED | null
    public bool? IsOpen { get; set; }
    public bool? IsSold { get; set; }
}

// ---------- PRODUCT DETAIL (CAT-05) ----------

/// <summary>GET /api/v1/product/{productId}</summary>
public sealed class ProductDetailResponseDto
{
    public ProductInfoDto? Product { get; set; }
    public CategoryInfoDto? Category { get; set; }
    public List<VariantInfoDto>? Variants { get; set; }
    public List<ProductImageDto>? Images { get; set; }
    public ProductRatingDto? Rating { get; set; }
}

public sealed class ProductInfoDto
{
    public string? ProductId { get; set; }
    public string? SellerId { get; set; }
    public string? CategoryId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Brand { get; set; }
    public string? SaleType { get; set; }
    public string? Status { get; set; }
    public string? StatusReason { get; set; }
    public object? Specifications { get; set; }
    public string? PublishedAt { get; set; }
}

public sealed class CategoryInfoDto
{
    public string? CategoryId { get; set; }
    public string? ParentId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int Level { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

public sealed class VariantInfoDto
{
    public string? VariantId { get; set; }
    public string? SkuCode { get; set; }
    public string? Name { get; set; }
    public decimal? Price { get; set; }
    public string? Status { get; set; }
    public bool IsDefault { get; set; }
    public List<AttributeDto>? Attributes { get; set; }
}

public sealed class ProductImageDto
{
    public string? ImageId { get; set; }
    public string? Url { get; set; }
    public string? AltText { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public string? VariantId { get; set; }
}

public sealed class ProductRatingDto
{
    public decimal? AvgRating { get; set; }
    public int ReviewCount { get; set; }
}

// ---------- PRODUCT MUTATIONS ----------

/// <summary>CAT-01: POST /api/v1/products — 201 Created</summary>
public sealed class ProductCreatedResponseDto
{
    public string? ProductId { get; set; }
    public string? Status { get; set; }
    public string? SaleType { get; set; }
    public List<VariantCreatedDto>? Variants { get; set; }
}

public sealed class VariantCreatedDto
{
    public string? VariantId { get; set; }
    public string? SkuCode { get; set; }
}

/// <summary>CAT-02: PATCH /api/v1/product/{productId} — 200 OK</summary>
public sealed class ProductUpdatedResponseDto
{
    public string? ProductId { get; set; }
    public string? CategoryId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Brand { get; set; }
    public string? SaleType { get; set; }
    public string? Status { get; set; }
    public object? Specifications { get; set; }
    public string? UpdatedAt { get; set; }
}

/// <summary>CAT-12: POST /api/v1/product/{productId}pics — 201 Created</summary>
public sealed class ProductImageAddResponseDto
{
    public List<ProductImageItemDto>? Items { get; set; }
    public PaginationDto? Pagination { get; set; }
}

public sealed class ProductImageItemDto
{
    public string? ImageId { get; set; }
    public string? Url { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>CAT-40: GET /api/v1/product/{productId}pics</summary>
public sealed class ProductImageListResponseDto
{
    public List<ProductImageDto>? Items { get; set; }
    public PaginationDto? Pagination { get; set; }
}

// ---------- VARIANT MUTATIONS ----------

/// <summary>CAT-07: POST /api/v1/product/{productId}/variants — 201 Created</summary>
public sealed class VariantCreatedResponseDto
{
    public string? VariantId { get; set; }
    public string? SkuCode { get; set; }
}

/// <summary>CAT-08: PATCH variant — 200 OK</summary>
public sealed class VariantUpdatedResponseDto
{
    public string? VariantId { get; set; }
}

/// <summary>Variant detail</summary>
public sealed class VariantDetailResponseDto
{
    public string? VariantId { get; set; }
    public string? SkuCode { get; set; }
}

// ---------- CATEGORY RESPONSES ----------

/// <summary>GET /api/v1/categories — item</summary>
public sealed class CategoryListItemDto
{
    public string? CategoryId { get; set; }
    public string? ParentId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int Level { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>CAT-31: POST /api/v1/categories — 201 Created</summary>
public sealed class CategoryCreatedResponseDto
{
    public string? CategoryId { get; set; }
    public string? Name { get; set; }
}

/// <summary>CAT-32: PUT /api/v1/categories/{categoryId} — 200 OK</summary>
public sealed class CategoryUpdatedResponseDto
{
    public string? CategoryId { get; set; }
    public string? Name { get; set; }
}

/// <summary>GET /api/v1/categories/{categoryId}</summary>
public sealed class CategoryDetailResponseDto
{
    public string? CategoryId { get; set; }
    public string? ParentId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int Level { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

// ---------- STOCK RESPONSES ----------

/// <summary>Stock-in response (simple message)</summary>
public sealed class StockInResponseDto
{
    public string? Message { get; set; }
}

// ---------- AUCTION PRODUCT RESPONSES ----------

/// <summary>CAT: POST /api/v1/products/auction — 201 Created</summary>
public sealed class AuctionProductCreatedResponseDto
{
    public string? ProductId { get; set; }
    public string? Name { get; set; }
}

/// <summary>GET auction product detail</summary>
public sealed class AuctionProductDetailResponseDto
{
    public string? ProductId { get; set; }
    public string? Name { get; set; }
}

// ---------- SHARED PAGINATION ----------

public sealed class PaginationDto
{
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasPreviousPage { get; set; }
    public bool HasNextPage { get; set; }
}


/// <summary>GET /api/v1/products/{id}/rating — product rating detail</summary>
public sealed class ProductRatingResponseDto
{
    public string? ProductId { get; set; }
    public double AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public ProductRatingSummaryDto? Summary { get; set; }
}

/// <summary>Rating breakdown by star count</summary>
public sealed class ProductRatingSummaryDto
{
    public int Star1 { get; set; }
    public int Star2 { get; set; }
    public int Star3 { get; set; }
    public int Star4 { get; set; }
    public int Star5 { get; set; }
}

/// <summary>GET /api/v1/products/{id}/rating/summary</summary>
public sealed class ProductRatingSummaryResponseDto
{
    public string? ProductId { get; set; }
    public double AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public int TotalOrders { get; set; }
}

/// <summary>GET /api/v1/products/{id}/status</summary>
public sealed class ProductStatusResponseDto
{
    public string? ProductId { get; set; }
    public string? Status { get; set; }
    public bool IsAvailable { get; set; }
    public int TotalStock { get; set; }
}

/// <summary>PATCH /api/v1/products/{id}/status — request body</summary>
public sealed class UpdateProductStatusDto
{
    public string? Status { get; set; }
    public string? StatusReason { get; set; }
    public string? Reason { get; set; }
}

/// <summary>StockIn variant response (alias for InventoryController)</summary>
public sealed class StockInVariantResponseDto
{
    public string? SkuId { get; set; }
    public string? Sku { get; set; }
    public int QuantityAdded { get; set; }
    public int NewStock { get; set; }
}

/// <summary>GET /api/v1/categories — paged category list wrapper</summary>
public sealed class CategoryListResponseDto
{
    public List<CategoryListItemDto>? Items { get; set; }
    public PaginationDto? Pagination { get; set; }
}
