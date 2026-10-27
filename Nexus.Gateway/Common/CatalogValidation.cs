namespace Nexus.Gateway.Common;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Nexus.Gateway.Dtos.Catalog;
using Nexus.Gateway.Responses;

/// <summary>
/// Gateway-owned validation for the F2 catalog contract. Throws <see cref="GatewayInputException"/>
/// (mapped to 400 VAL_INVALID_INPUT) so controllers stay thin.
/// </summary>
public static class CatalogValidation
{
    public const string ErrorCode = "VAL_INVALID_INPUT";
    public const int MaxPageSize = 100;
    public const int MaxStockInLines = 100;
    public const int MaxImages = 5;

    public static readonly string[] ProductStatuses = ["DRAFT", "ACTIVE", "INACTIVE"];
    public static readonly string[] AuctionStatuses = ["DRAFT", "SCHEDULED", "RUNNING", "ENDED", "SETTLED", "FAILED", "CANCELLED"];
    public static readonly string[] StatusReasons = ["SELLER_PAUSED", "DISCONTINUED", "ADMIN_LOCKED", "OTHER"];
    public static readonly string[] SaleTypes = ["REGULAR", "AUCTION"];
    public static readonly string[] SortByValues = ["newest", "priceAsc", "priceDesc", "rating"];
    public static readonly string[] SortDirValues = ["asc", "desc"];
    public static readonly string[] RatingSortByValues = ["newest", "ratingDesc", "ratingAsc"];

    // ---------------------------------------------------------------- ids

    public static void EnsureId(string? value, string field, string label)
    {
        if (!Guid.TryParse(value, out _))
            throw new GatewayInputException($"The {label} must be a valid UUID.", field, ErrorCode);
    }

    public static void EnsureProductId(string? value) => EnsureId(value, "productId", "product ID");

    public static void EnsureVariantId(string? value) => EnsureId(value, "variantId", "variant ID");

    public static void EnsureCategoryId(string? value) => EnsureId(value, "categoryId", "category ID");

    public static void EnsureAuctionId(string? value) => EnsureId(value, "auctionId", "auction ID");

    // ---------------------------------------------------------------- paging / filters

    public static void EnsurePaged(int page, int pageSize)
    {
        if (page < 1 || pageSize < 1 || pageSize > MaxPageSize)
            throw new GatewayInputException($"Page must be at least 1 and pageSize must be between 1 and {MaxPageSize}.", "Pagination", ErrorCode);
    }

    public static void EnsureOptionalSortBy(string? sortBy, IReadOnlyList<string>? allowed = null)
    {
        if (string.IsNullOrWhiteSpace(sortBy)) return;
        var values = allowed ?? SortByValues;
        if (!values.Contains(sortBy, StringComparer.OrdinalIgnoreCase))
            throw new GatewayInputException($"SortBy must be one of: {string.Join(", ", values)}.", "sortBy", ErrorCode);
    }

    public static void EnsureOptionalSortDir(string? sortDir, string field = "sortDir")
    {
        if (string.IsNullOrWhiteSpace(sortDir)) return;
        if (!SortDirValues.Contains(sortDir, StringComparer.OrdinalIgnoreCase))
            throw new GatewayInputException($"SortDir must be one of: {string.Join(", ", SortDirValues)}.", field, ErrorCode);
    }

    public static void EnsureOptionalSaleType(string? saleType)
    {
        if (string.IsNullOrWhiteSpace(saleType)) return;
        if (!SaleTypes.Contains(saleType, StringComparer.OrdinalIgnoreCase))
            throw new GatewayInputException($"SaleType must be one of: {string.Join(", ", SaleTypes)}.", "saleType", ErrorCode);
    }

    public static void EnsureOptionalStatus(string? status, string field = "status")
    {
        if (string.IsNullOrWhiteSpace(status)) return;
        if (!ProductStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
            throw new GatewayInputException($"Status must be one of: {string.Join(", ", ProductStatuses)}.", field, ErrorCode);
    }

    public static void EnsureOptionalAuctionStatus(string? status, string field = "status")
    {
        if (string.IsNullOrWhiteSpace(status)) return;
        if (!AuctionStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
            throw new GatewayInputException($"Status must be one of: {string.Join(", ", AuctionStatuses)}.", field, ErrorCode);
    }

    public static void EnsureRatingRange(decimal? minRating)
    {
        if (minRating is null) return;
        if (minRating < 0 || minRating > 5)
            throw new GatewayInputException("MinRating must be between 0 and 5.", "minRating", ErrorCode);
    }

    public static void EnsurePriceRange(decimal? minPrice, decimal? maxPrice)
    {
        if (minPrice is not null && minPrice < 0)
            throw new GatewayInputException("MinPrice cannot be negative.", "minPrice", ErrorCode);
        if (maxPrice is not null && maxPrice < 0)
            throw new GatewayInputException("MaxPrice cannot be negative.", "maxPrice", ErrorCode);
        if (minPrice is not null && maxPrice is not null && minPrice > maxPrice)
            throw new GatewayInputException("MinPrice cannot be greater than MaxPrice.", "priceRange", ErrorCode);
    }

    // ---------------------------------------------------------------- products

    public static void EnsureCreateProduct(CreateProductDto body)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            throw new GatewayInputException("The product name is required.", "name", ErrorCode);
        if (body.CategoryId is null || body.CategoryId == Guid.Empty)
            throw new GatewayInputException("A valid categoryId is required.", "categoryId", ErrorCode);
        if (body.Variants is null || body.Variants.Count == 0)
            throw new GatewayInputException("At least one product variant is required.", "variants", ErrorCode);

        foreach (var variant in body.Variants)
        {
            if (string.IsNullOrWhiteSpace(variant.SkuCode))
                throw new GatewayInputException("Variant.skuCode is required.", "skuCode", ErrorCode);
            if (string.IsNullOrWhiteSpace(variant.Name))
                throw new GatewayInputException("Variant.name is required.", "name", ErrorCode);
            if (variant.Price <= 0)
                throw new GatewayInputException("Variant.price must be greater than 0.", "price", ErrorCode);
            EnsureAttributes(variant.Attributes, "variant.attributes");
        }
    }

    public static void EnsureUpdateProduct(UpdateProductDto body)
    {
        if (body is null ||
            (string.IsNullOrWhiteSpace(body.Name) &&
             string.IsNullOrWhiteSpace(body.Description) &&
             string.IsNullOrWhiteSpace(body.Brand) &&
             (!body.CategoryId.HasValue || body.CategoryId == Guid.Empty) &&
             (!body.Specifications.HasValue || body.Specifications.Value.ValueKind == JsonValueKind.Undefined)))
        {
            throw new GatewayInputException("At least one product property must be provided to update.", "body", ErrorCode);
        }

        if (body.CategoryId.HasValue && body.CategoryId.Value == Guid.Empty)
        {
            throw new GatewayInputException("A valid categoryId is required.", "categoryId", ErrorCode);
        }
    }

    public static void EnsureProductStatus(UpdateProductStatusDto body)
    {
        EnsureOptionalStatus(body.Status);
        var reason = body.StatusReason ?? body.Reason;
        if (string.Equals(body.Status, "INACTIVE", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(reason))
        {
            throw new GatewayInputException("Reason is required when status is INACTIVE.", "reason", ErrorCode);
        }
        if (!string.IsNullOrWhiteSpace(reason) &&
            !StatusReasons.Contains(reason, StringComparer.OrdinalIgnoreCase))
        {
            throw new GatewayInputException($"Reason must be one of: {string.Join(", ", StatusReasons)}.", "reason", ErrorCode);
        }
    }

    // ---------------------------------------------------------------- variants

    public static void EnsureCreateVariant(string productId, string? skuCode, string? name, decimal? price, List<AttributeDto>? attributes)
    {
        EnsureProductId(productId);
        if (string.IsNullOrWhiteSpace(skuCode))
            throw new GatewayInputException("The skuCode is required.", "skuCode", ErrorCode);
        if (string.IsNullOrWhiteSpace(name))
            throw new GatewayInputException("The variant name is required.", "name", ErrorCode);
        if (price is null || price <= 0)
            throw new GatewayInputException("The price must be greater than 0.", "price", ErrorCode);
        EnsureAttributes(attributes, "attributes");
    }

    public static void EnsureCreateVariant(string productId, string? skuCode, string? name, decimal price, List<AttributeDto>? attributes)
        => EnsureCreateVariant(productId, skuCode, name, (decimal?)price, attributes);

    public static void EnsureUpdateVariant(string variantId, string? name, decimal? price, List<AttributeDto>? attributes)
    {
        EnsureVariantId(variantId);
        if (price.HasValue && price <= 0)
            throw new GatewayInputException("The price must be greater than 0.", "price", ErrorCode);
        EnsureAttributes(attributes, "attributes");
    }

    public static void EnsureUpdateVariant(string variantId, string? name, decimal price, List<AttributeDto>? attributes)
        => EnsureUpdateVariant(variantId, name, (decimal?)price, attributes);

    private static void EnsureAttributes(List<AttributeDto>? attributes, string field)
    {
        if (attributes is null) return;
        foreach (var attribute in attributes)
        {
            if (string.IsNullOrWhiteSpace(attribute.AttrName))
                throw new GatewayInputException("Attribute name is required.", field, ErrorCode);
            if (string.IsNullOrWhiteSpace(attribute.AttrValue))
                throw new GatewayInputException("Attribute value is required.", field, ErrorCode);
        }
    }

    // ---------------------------------------------------------------- images

    public static void EnsureImageFiles(IReadOnlyList<IFormFile>? files)
    {
        if (files is null || files.Count == 0)
            throw new GatewayInputException("At least one image file is required.", "files", ErrorCode);
        if (files.Count > MaxImages)
            throw new GatewayInputException($"At most {MaxImages} images can be uploaded at once.", "files", ErrorCode);
    }

    public static void EnsureImageFiles(IFormFileCollection? files)
        => EnsureImageFiles((IReadOnlyList<IFormFile>?)files);

    public static void EnsureImageIds(List<Guid>? ids)
    {
        if (ids is null || ids.Count == 0)
            throw new GatewayInputException("At least one image ID is required.", "imageIds", ErrorCode);
    }

    public static void EnsureImageIds(string? ids)
    {
        if (string.IsNullOrWhiteSpace(ids))
            throw new GatewayInputException("At least one image ID is required.", "ids", ErrorCode);
    }

    // ---------------------------------------------------------------- inventory

    public static void EnsureStockIn(StockInDto body)
    {
        if (body.Items is null || body.Items.Count == 0)
            throw new GatewayInputException("At least one stock-in line is required.", "items", ErrorCode);
        if (body.Items.Count > MaxStockInLines)
            throw new GatewayInputException($"A single stock-in request can contain at most {MaxStockInLines} lines.", "items", ErrorCode);

        foreach (var item in body.Items)
        {
            if (item.SkuId is null || item.SkuId == Guid.Empty)
                throw new GatewayInputException("The skuId is required.", "skuId", ErrorCode);
            if (item.Quantity <= 0)
                throw new GatewayInputException("Quantity must be greater than 0.", "quantity", ErrorCode);
        }
    }

    public static void EnsureStockInVariant(string variantId, int quantity)
    {
        EnsureVariantId(variantId);
        if (quantity <= 0)
            throw new GatewayInputException("Quantity must be greater than 0.", "quantity", ErrorCode);
    }

    // ---------------------------------------------------------------- categories

    public static void EnsureCreateCategory(CreateCategoryDto body)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            throw new GatewayInputException("The category name is required.", "name", ErrorCode);
        if (body.ParentId == Guid.Empty)
            throw new GatewayInputException("A valid parentId is required when it is supplied.", "parentId", ErrorCode);
    }

    public static void EnsureUpdateCategory(UpdateCategoryDto body)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            throw new GatewayInputException("The category name is required.", "name", ErrorCode);
        if (body.ParentId == Guid.Empty)
            throw new GatewayInputException("A valid parentId is required when it is supplied.", "parentId", ErrorCode);
    }

    // ---------------------------------------------------------------- auction products

    public static void EnsureCreateAuctionProduct(CreateAuctionProductDto body)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            throw new GatewayInputException("The product name is required.", "name", ErrorCode);
        if (body.CategoryId is null || body.CategoryId == Guid.Empty)
            throw new GatewayInputException("A valid categoryId is required.", "categoryId", ErrorCode);
        if (body.Variant is null)
            throw new GatewayInputException("A single variant is required.", "variant", ErrorCode);
        if (string.IsNullOrWhiteSpace(body.Variant.SkuCode))
            throw new GatewayInputException("Variant.skuCode is required.", "skuCode", ErrorCode);
        if (string.IsNullOrWhiteSpace(body.Variant.Name))
            throw new GatewayInputException("Variant.name is required.", "name", ErrorCode);
        EnsureAttributes(body.Variant.Attributes, "variant.attributes");
    }
}
