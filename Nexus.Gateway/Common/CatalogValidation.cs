namespace Nexus.Gateway.Common;

using System.Text.Json;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Http;
using Nexus.Gateway.Dtos.Catalog;
using Nexus.Gateway.Responses;
using ShoppingCart.Contracts.V1;

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
            throw new GatewayInputException($"page must be at least 1 and pageSize must be between 1 and {MaxPageSize}.", "Pagination", ErrorCode);
    }

    public static void EnsureOptionalSortBy(string? sortBy, IReadOnlyList<string>? allowed = null)
    {
        if (string.IsNullOrWhiteSpace(sortBy)) return;
        var values = allowed ?? SortByValues;
        if (!values.Contains(sortBy, StringComparer.OrdinalIgnoreCase))
            throw new GatewayInputException($"sortBy must be one of: {string.Join(", ", values)}.", "sortBy", ErrorCode);
    }

    public static void EnsureOptionalSortDir(string? sortDir, string field = "sortDir")
    {
        if (string.IsNullOrWhiteSpace(sortDir)) return;
        if (!SortDirValues.Contains(sortDir, StringComparer.OrdinalIgnoreCase))
            throw new GatewayInputException($"sortDir must be one of: {string.Join(", ", SortDirValues)}.", field, ErrorCode);
    }

    public static void EnsureOptionalSaleType(string? saleType)
    {
        if (string.IsNullOrWhiteSpace(saleType)) return;
        if (!SaleTypes.Contains(saleType, StringComparer.OrdinalIgnoreCase))
            throw new GatewayInputException($"saleType must be one of: {string.Join(", ", SaleTypes)}.", "saleType", ErrorCode);
    }

    public static void EnsureOptionalStatus(string? status, string field = "status")
    {
        if (string.IsNullOrWhiteSpace(status)) return;
        if (!ProductStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
            throw new GatewayInputException($"status must be one of: {string.Join(", ", ProductStatuses)}.", field, ErrorCode);
    }

    /// <summary>
    /// Auction status filter for the catalog-side auction product routes. The vocabulary comes from
    /// auction.proto (AuctionStatus) and is deliberately different from the product status set.
    /// </summary>
    public static void EnsureOptionalAuctionStatus(string? status, string field = "status")
    {
        if (string.IsNullOrWhiteSpace(status)) return;
        if (!AuctionStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
            throw new GatewayInputException($"status must be one of: {string.Join(", ", AuctionStatuses)}.", field, ErrorCode);
    }

    public static void EnsureRatingRange(decimal? minRating)
    {
        if (minRating is null) return;
        if (minRating < 0 || minRating > 5)
            throw new GatewayInputException("minRating must be between 0 and 5.", "minRating", ErrorCode);
    }

    public static void EnsurePriceRange(decimal? minPrice, decimal? maxPrice)
    {
        if (minPrice is < 0 || maxPrice is < 0)
            throw new GatewayInputException("minPrice and maxPrice cannot be negative.", "price", ErrorCode);
        if (minPrice.HasValue && maxPrice.HasValue && minPrice > maxPrice)
            throw new GatewayInputException("minPrice cannot be greater than maxPrice.", "price", ErrorCode);
    }

    // ---------------------------------------------------------------- products

    public static void EnsureCreateProduct(CreateProductDto body)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            throw new GatewayInputException("The product name is required.", "name", ErrorCode);
        if (body.CategoryId is null || body.CategoryId == Guid.Empty)
            throw new GatewayInputException("A valid categoryId is required.", "categoryId", ErrorCode);
        if (body.Variants is null || body.Variants.Count == 0)
            throw new GatewayInputException("At least one variant is required.", "variants", ErrorCode);

        var defaults = 0;
        for (var i = 0; i < body.Variants.Count; i++)
        {
            var variant = body.Variants[i];
            if (string.IsNullOrWhiteSpace(variant.SkuCode))
                throw new GatewayInputException($"variants[{i}].skuCode is required.", "skuCode", ErrorCode);
            if (string.IsNullOrWhiteSpace(variant.Name))
                throw new GatewayInputException($"variants[{i}].name is required.", "name", ErrorCode);
            if (variant.Price is null or <= 0)
                throw new GatewayInputException($"variants[{i}].price must be greater than 0.", "price", ErrorCode);
            if (variant.IsActive) defaults++;
            EnsureAttributes(variant.Attributes, $"variants[{i}].attributes");
        }

        if (defaults != 1)
            throw new GatewayInputException("Exactly one variant must be flagged as the default (isActive = true).", "variants", ErrorCode);
    }

    public static void EnsureUpdateProduct(UpdateProductDto body)
    {
        if (body.CategoryId is null && body.Name is null && body.Description is null && body.Brand is null && body.Specifications is null)
            throw new GatewayInputException("At least one field must be provided.", "body", ErrorCode);
        if (body.Name is not null && string.IsNullOrWhiteSpace(body.Name))
            throw new GatewayInputException("The product name cannot be empty.", "name", ErrorCode);
        if (body.CategoryId == Guid.Empty)
            throw new GatewayInputException("A valid categoryId is required.", "categoryId", ErrorCode);
    }

    public static void EnsureProductStatus(UpdateProductStatusDto body)
    {
        if (string.IsNullOrWhiteSpace(body.Status) || !ProductStatuses.Contains(body.Status, StringComparer.OrdinalIgnoreCase))
            throw new GatewayInputException($"status must be one of: {string.Join(", ", ProductStatuses)}.", "status", ErrorCode);

        var inactive = string.Equals(body.Status, "INACTIVE", StringComparison.OrdinalIgnoreCase);
        if (inactive && string.IsNullOrWhiteSpace(body.StatusReason))
            throw new GatewayInputException("statusReason is required when the status becomes INACTIVE.", "statusReason", ErrorCode);
        if (!inactive && !string.IsNullOrWhiteSpace(body.StatusReason))
            throw new GatewayInputException("statusReason is only accepted when the status becomes INACTIVE.", "statusReason", ErrorCode);
        if (inactive && !StatusReasons.Contains(body.StatusReason!, StringComparer.OrdinalIgnoreCase))
            throw new GatewayInputException($"statusReason must be one of: {string.Join(", ", StatusReasons)}.", "statusReason", ErrorCode);
    }

    public static void EnsureAttributes(List<AttributeDto>? attributes, string field)
    {
        if (attributes is null) return;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var attribute in attributes)
        {
            if (string.IsNullOrWhiteSpace(attribute.AttrName) || string.IsNullOrWhiteSpace(attribute.AttrValue))
                throw new GatewayInputException($"{field} requires both attrName and attrValue.", field, ErrorCode);
            if (!seen.Add(attribute.AttrName))
                throw new GatewayInputException($"attrName '{attribute.AttrName}' is duplicated.", field, ErrorCode);
        }
    }

    // ---------------------------------------------------------------- images

    public static void EnsureImageFiles(List<IFormFile>? files)
    {
        if (files is null || files.Count == 0)
            throw new GatewayInputException("At least one image file is required.", "files", ErrorCode);
        if (files.Count > MaxImages)
            throw new GatewayInputException($"At most {MaxImages} images can be uploaded at once.", "files", ErrorCode);
    }

    public static void EnsureImageIds(string? ids)
    {
        if (string.IsNullOrWhiteSpace(ids))
            throw new GatewayInputException("The ids query parameter is required.", "ids", ErrorCode);

        foreach (var id in ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Guid.TryParse(id, out _))
                throw new GatewayInputException($"'{id}' is not a valid image ID.", "ids", ErrorCode);
        }
    }

    public static string[] ParseImageIds(string ids) =>
        ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // ---------------------------------------------------------------- variants

    public static void EnsureCreateVariant(string? productId, string? skuCode, string? name, decimal? price, List<AttributeDto>? attributes)
    {
        EnsureProductId(productId);
        if (string.IsNullOrWhiteSpace(skuCode))
            throw new GatewayInputException("skuCode is required.", "skuCode", ErrorCode);
        if (string.IsNullOrWhiteSpace(name))
            throw new GatewayInputException("name is required.", "name", ErrorCode);
        if (price is null or <= 0)
            throw new GatewayInputException("price must be greater than 0.", "price", ErrorCode);
        EnsureAttributes(attributes, "attributes");
    }

    public static void EnsureUpdateVariant(string? variantId, string? name, decimal? price, List<AttributeDto>? attributes)
    {
        EnsureVariantId(variantId);
        if (name is null && price is null)
            throw new GatewayInputException("At least one field must be provided.", "body", ErrorCode);
        if (name is not null && string.IsNullOrWhiteSpace(name))
            throw new GatewayInputException("name cannot be empty.", "name", ErrorCode);
        if (price is <= 0)
            throw new GatewayInputException("price must be greater than 0.", "price", ErrorCode);
        EnsureAttributes(attributes, "attributes");
    }

    // ---------------------------------------------------------------- inventory

    public static void EnsureStockIn(StockInDto body)
    {
        if (body.Items is null || body.Items.Count == 0)
            throw new GatewayInputException("At least one stock-in line is required.", "items", ErrorCode);
        if (body.Items.Count > MaxStockInLines)
            throw new GatewayInputException($"At most {MaxStockInLines} stock-in lines are allowed.", "items", ErrorCode);

        var seen = new HashSet<Guid>();
        foreach (var item in body.Items)
        {
            if (item.SkuId is null || item.SkuId == Guid.Empty)
                throw new GatewayInputException("Every stock-in line requires a valid skuId.", "skuId", ErrorCode);
            if (item.Quantity <= 0)
                throw new GatewayInputException("quantity must be greater than 0.", "quantity", ErrorCode);
            if (!seen.Add(item.SkuId.Value))
                throw new GatewayInputException($"skuId '{item.SkuId}' appears more than once.", "skuId", ErrorCode);
        }
    }

    public static void EnsureStockInVariant(string? variantId, int quantity)
    {
        EnsureVariantId(variantId);
        if (quantity <= 0)
            throw new GatewayInputException("quantity must be greater than 0.", "quantity", ErrorCode);
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
            throw new GatewayInputException("variant.skuCode is required.", "skuCode", ErrorCode);
        if (string.IsNullOrWhiteSpace(body.Variant.Name))
            throw new GatewayInputException("variant.name is required.", "name", ErrorCode);
        EnsureAttributes(body.Variant.Attributes, "variant.attributes");
    }

    // ---------------------------------------------------------------- helpers

    public static Struct ParseSpecifications(JsonElement? specifications)
    {
        if (specifications is null || specifications.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return new Struct();

        return Struct.Parser.ParseJson(specifications.Value.GetRawText());
    }

    public static RepeatedField<AttributeItem> ToAttributes(List<AttributeDto>? attributes)
    {
        var items = new RepeatedField<AttributeItem>();
        if (attributes is null) return items;

        foreach (var attribute in attributes)
            items.Add(new AttributeItem { AttrName = attribute.AttrName ?? string.Empty, AttrValue = attribute.AttrValue ?? string.Empty });

        return items;
    }

    /// <summary>Identity of the authenticated caller, taken from the verified JWT only.</summary>
    public static CallerContext Caller(HttpContext context)
    {
        var caller = new CallerContext { UserId = context.User.FindFirst("sub")?.Value ?? string.Empty };
        foreach (var role in context.GetRoles()) caller.Roles.Add(role);
        return caller;
    }
}
