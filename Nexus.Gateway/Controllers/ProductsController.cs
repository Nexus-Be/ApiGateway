namespace Nexus.Gateway.Controllers;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.Gateway.Common;
using Nexus.Gateway.Dispatching;
using Nexus.Gateway.Dtos.Catalog;
using Nexus.Gateway.Responses;
using Nexus.Gateway.Security;

[ApiController]
[Route("api/v1")]
[BackendTag("catalog")]
[EnableRateLimiting("api")]
public sealed class ProductsController : GatewayBaseController
{
    private const string Service = "catalog";

    public ProductsController(IGatewayDispatcher dispatcher)
        : base(dispatcher)
    {
    }

    /// <summary>Lists products with optional filtering and pagination.</summary>
    [HttpGet("products")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PagedData<ProductListItemDto>>), StatusCodes.Status200OK)]
    public Task<IResult> ListProducts(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] string? keyword,
        [FromQuery] Guid? categoryId,
        [FromQuery] string? status,
        [FromQuery] Guid? sellerId,
        [FromQuery] string? saleType,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir)
    {
        CatalogValidation.EnsurePaged(pagination.Page, pagination.PageSize);
        CatalogValidation.EnsureOptionalStatus(status);
        CatalogValidation.EnsureOptionalSaleType(saleType);
        CatalogValidation.EnsureOptionalSortBy(sortBy);
        CatalogValidation.EnsureOptionalSortDir(sortDir);

        return ForwardPagedAsync(
            Service,
            "/api/v1/products",
            pagination.Page,
            pagination.PageSize,
            HttpMethod.Get,
            mockSeed: "products",
            queryParams: new Dictionary<string, string?>
            {
                ["page"] = pagination.Page.ToString(),
                ["pageSize"] = pagination.PageSize.ToString(),
                ["keyword"] = keyword,
                ["categoryId"] = categoryId?.ToString(),
                ["status"] = status,
                ["sellerId"] = sellerId?.ToString(),
                ["saleType"] = saleType,
                ["sortBy"] = sortBy,
                ["sortDir"] = sortDir
            }
        );
    }

    /// <summary>Searches products with query parameters.</summary>
    [HttpGet("products/search")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PagedData<ProductListItemDto>>), StatusCodes.Status200OK)]
    public Task<IResult> SearchProducts(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] string? keyword,
        [FromQuery] Guid? categoryId,
        [FromQuery] string? status,
        [FromQuery] Guid? sellerId,
        [FromQuery] string? saleType,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir)
    {
        return ListProducts(pagination, keyword, categoryId, status, sellerId, saleType, sortBy, sortDir);
    }

    /// <summary>Searches seller products with filters.</summary>
    [HttpGet("product/search")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<PagedData<ProductListItemDto>>), StatusCodes.Status200OK)]
    public Task<IResult> SearchProducts(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] string? keyword,
        [FromQuery] Guid? categoryId,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] string? brand,
        [FromQuery] string? saleType,
        [FromQuery] string? attributes,
        [FromQuery] decimal? minRating,
        [FromQuery] string? status,
        [FromQuery] string? sortBy)
    {
        CatalogValidation.EnsurePaged(pagination.Page, pagination.PageSize);
        CatalogValidation.EnsurePriceRange(minPrice, maxPrice);
        CatalogValidation.EnsureRatingRange(minRating);
        CatalogValidation.EnsureOptionalSaleType(saleType);
        CatalogValidation.EnsureOptionalStatus(status);
        CatalogValidation.EnsureOptionalSortBy(sortBy);
        EnsureAttributeFilter(attributes);

        return ForwardPagedAsync(
            Service,
            "/api/v1/product/search",
            pagination.Page,
            pagination.PageSize,
            HttpMethod.Get,
            mockSeed: "product-search",
            queryParams: new Dictionary<string, string?>
            {
                ["page"] = pagination.Page.ToString(),
                ["pageSize"] = pagination.PageSize.ToString(),
                ["keyword"] = keyword,
                ["categoryId"] = categoryId?.ToString(),
                ["minPrice"] = minPrice?.ToString(),
                ["maxPrice"] = maxPrice?.ToString(),
                ["brand"] = brand,
                ["saleType"] = saleType,
                ["attributes"] = attributes,
                ["minRating"] = minRating?.ToString(),
                ["status"] = status,
                ["sortBy"] = sortBy
            }
        );
    }

    /// <summary>Gets product details by product ID.</summary>
    [HttpGet("product/{productId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<ProductDetailResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> GetProduct([FromRoute] string productId, [FromQuery] string? auctionId)
    {
        CatalogValidation.EnsureProductId(productId);
        if (!string.IsNullOrWhiteSpace(auctionId)) CatalogValidation.EnsureAuctionId(auctionId);

        var queryParams = string.IsNullOrWhiteSpace(auctionId)
            ? null
            : new Dictionary<string, string?> { ["auctionId"] = auctionId };

        return ForwardAsync(
            Service,
            $"/api/v1/product/{productId}",
            HttpMethod.Get,
            mockSeed: "product",
            queryParams: queryParams
        );
    }

    /// <summary>Creates a new product with its first variant.</summary>
    [HttpPost("products")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<ProductCreatedResponseDto>), StatusCodes.Status201Created)]
    public Task<IResult> CreateProduct([FromBody] CreateProductDto body)
    {
        CatalogValidation.EnsureCreateProduct(body);

        return ForwardAsync(
            Service,
            "/api/v1/products",
            HttpMethod.Post,
            body: body,
            mockSeed: "product-create",
            successMessage: "Product created successfully.",
            successStatusCode: StatusCodes.Status201Created
        );
    }

    /// <summary>Updates product information.</summary>
    [HttpPatch("product/{productId}")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<ProductUpdatedResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> UpdateProduct([FromRoute] string productId, [FromBody] UpdateProductDto body)
    {
        CatalogValidation.EnsureProductId(productId);
        CatalogValidation.EnsureUpdateProduct(body);

        return ForwardAsync(
            Service,
            $"/api/v1/product/{productId}",
            HttpMethod.Patch,
            body: body,
            mockSeed: "product-update"
        );
    }

    /// <summary>Deletes a product by product ID.</summary>
    [HttpDelete("product/{productId}")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IResult> DeleteProduct([FromRoute] string productId)
    {
        CatalogValidation.EnsureProductId(productId);

        return ForwardNoContentAsync(
            Service,
            $"/api/v1/product/{productId}",
            HttpMethod.Delete,
            mockSeed: "product-delete"
        );
    }

    /// <summary>Uploads images for a product.</summary>
    [HttpPost("product/{productId}pics")]
    [Authorize(GatewayPolicies.Seller)]
    [RequestSizeLimit(20 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<ProductImageAddResponseDto>), StatusCodes.Status201Created)]
    public Task<IResult> AddProductImages([FromRoute] string productId, [FromForm] AddProductImagesForm form)
    {
        CatalogValidation.EnsureProductId(productId);
        CatalogValidation.EnsureImageFiles(form.Files);
        if (form.VariantId.HasValue) CatalogValidation.EnsureVariantId(form.VariantId.Value.ToString());

        return ForwardAsync(
            Service,
            $"/api/v1/product/{productId}pics",
            HttpMethod.Post,
            mockSeed: "product-images-add",
            successMessage: "Images uploaded successfully.",
            successStatusCode: StatusCodes.Status201Created
        );
    }

    /// <summary>Lists product images with optional variant filter.</summary>
    [HttpGet("product/{productId}pics")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<ProductImageListResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> ListProductImages([FromRoute] string productId, [FromQuery] string? variantId)
    {
        CatalogValidation.EnsureProductId(productId);
        if (!string.IsNullOrWhiteSpace(variantId)) CatalogValidation.EnsureVariantId(variantId);

        var queryParams = string.IsNullOrWhiteSpace(variantId)
            ? null
            : new Dictionary<string, string?> { ["variantId"] = variantId };

        return ForwardAsync(
            Service,
            $"/api/v1/product/{productId}pics",
            HttpMethod.Get,
            mockSeed: "product-images",
            queryParams: queryParams
        );
    }

    /// <summary>Deletes specified product images.</summary>
    [HttpDelete("product/{productId}pics")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IResult> DeleteProductImages([FromRoute] string productId, [FromQuery] string? ids)
    {
        CatalogValidation.EnsureProductId(productId);
        CatalogValidation.EnsureImageIds(ids);

        var queryParams = new Dictionary<string, string?> { ["ids"] = ids };

        return ForwardNoContentAsync(
            Service,
            $"/api/v1/product/{productId}pics",
            HttpMethod.Delete,
            mockSeed: "product-images-delete",
            queryParams: queryParams
        );
    }

    /// <summary>Gets average rating summary for a product.</summary>
    [HttpGet("products/{productId}/totalRating")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<ProductRatingSummaryResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> GetProductTotalRating([FromRoute] string productId)
    {
        CatalogValidation.EnsureProductId(productId);

        return ForwardAsync(
            Service,
            $"/api/v1/products/{productId}/totalRating",
            HttpMethod.Get,
            mockSeed: "product-rating-total"
        );
    }

    /// <summary>Gets reviews and rating distribution for a product.</summary>
    [HttpGet("products/{productId}/rating")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<ProductRatingResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> GetProductRating(
        [FromRoute] string productId,
        [FromQuery] PaginationRequest pagination,
        [FromQuery] int? rating,
        [FromQuery] string? sortBy)
    {
        CatalogValidation.EnsureProductId(productId);
        CatalogValidation.EnsurePaged(pagination.Page, pagination.PageSize);
        if (rating is < 1 or > 5)
            throw new GatewayInputException("Rating must be between 1 and 5.", "rating", CatalogValidation.ErrorCode);
        CatalogValidation.EnsureOptionalSortBy(sortBy, CatalogValidation.RatingSortByValues);

        return ForwardAsync(
            Service,
            $"/api/v1/products/{productId}/rating",
            HttpMethod.Get,
            mockSeed: "product-rating",
            queryParams: new Dictionary<string, string?>
            {
                ["page"] = pagination.Page.ToString(),
                ["pageSize"] = pagination.PageSize.ToString(),
                ["rating"] = rating?.ToString(),
                ["sortBy"] = sortBy
            }
        );
    }

    /// <summary>Updates product status.</summary>
    [HttpPatch("product/{productId}/status")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<ProductStatusResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> UpdateProductStatus([FromRoute] string productId, [FromBody] UpdateProductStatusDto body)
    {
        CatalogValidation.EnsureProductId(productId);
        CatalogValidation.EnsureProductStatus(body);

        return ForwardAsync(
            Service,
            $"/api/v1/product/{productId}/status",
            HttpMethod.Patch,
            body: body,
            mockSeed: "product-status"
        );
    }

    /// <summary>Creates a variant for a product.</summary>
    [HttpPost("product/{productId}/variants")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<VariantCreatedResponseDto>), StatusCodes.Status201Created)]
    public Task<IResult> CreateVariant([FromRoute] string productId, [FromBody] CreateVariantDto body)
    {
        CatalogValidation.EnsureCreateVariant(productId, body.SkuCode, body.Name, body.Price, body.Attributes);

        return ForwardAsync(
            Service,
            $"/api/v1/product/{productId}/variants",
            HttpMethod.Post,
            body: body,
            mockSeed: "variant-create",
            successMessage: "Variant created successfully.",
            successStatusCode: StatusCodes.Status201Created
        );
    }

    /// <summary>Deletes a product variant.</summary>
    [HttpDelete("variant/{variantId}")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IResult> DeleteVariant([FromRoute] string variantId)
    {
        CatalogValidation.EnsureVariantId(variantId);

        return ForwardNoContentAsync(
            Service,
            $"/api/v1/variant/{variantId}",
            HttpMethod.Delete,
            mockSeed: "variant-delete"
        );
    }

    /// <summary>Updates a product variant.</summary>
    [HttpPatch("variant/{variantId}")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<VariantUpdatedResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> UpdateVariant([FromRoute] string variantId, [FromBody] UpdateVariantDto body)
    {
        CatalogValidation.EnsureUpdateVariant(variantId, body.Name, body.Price, body.Attributes);

        return ForwardAsync(
            Service,
            $"/api/v1/variant/{variantId}",
            HttpMethod.Patch,
            body: body,
            mockSeed: "variant-update"
        );
    }

    /// <summary>Gets product variant details.</summary>
    [HttpGet("variant/{variantId}")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<VariantDetailResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> GetVariant([FromRoute] string variantId)
    {
        CatalogValidation.EnsureVariantId(variantId);

        return ForwardAsync(
            Service,
            $"/api/v1/variant/{variantId}",
            HttpMethod.Get,
            mockSeed: "variant"
        );
    }

    /// <summary>Creates an auction product.</summary>
    [HttpPost("product/auction")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<AuctionProductCreatedResponseDto>), StatusCodes.Status201Created)]
    public Task<IResult> CreateAuctionProduct([FromBody] CreateAuctionProductDto body)
    {
        CatalogValidation.EnsureCreateAuctionProduct(body);

        return ForwardAsync(
            Service,
            "/api/v1/product/auction",
            HttpMethod.Post,
            body: body,
            mockSeed: "auction-product-create",
            successMessage: "Auction product created successfully.",
            successStatusCode: StatusCodes.Status201Created
        );
    }

    /// <summary>Searches seller auction products.</summary>
    [HttpGet("product/auction/search")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<PagedData<ProductListItemDto>>), StatusCodes.Status200OK)]
    public Task<IResult> SearchAuctionProducts(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] string? keyword,
        [FromQuery] Guid? categoryId,
        [FromQuery] string? status,
        [FromQuery] string? brand,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir)
    {
        CatalogValidation.EnsurePaged(pagination.Page, pagination.PageSize);
        CatalogValidation.EnsureOptionalAuctionStatus(status);
        CatalogValidation.EnsureOptionalSortBy(sortBy);
        CatalogValidation.EnsureOptionalSortDir(sortDir);

        return ForwardPagedAsync(
            Service,
            "/api/v1/product/auction/search",
            pagination.Page,
            pagination.PageSize,
            HttpMethod.Get,
            mockSeed: "auction-product-search",
            queryParams: new Dictionary<string, string?>
            {
                ["page"] = pagination.Page.ToString(),
                ["pageSize"] = pagination.PageSize.ToString(),
                ["keyword"] = keyword,
                ["categoryId"] = categoryId?.ToString(),
                ["status"] = status,
                ["brand"] = brand,
                ["sortBy"] = sortBy,
                ["sortDir"] = sortDir
            }
        );
    }

    /// <summary>Lists seller auction products.</summary>
    [HttpGet("product/auctions")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<PagedData<ProductListItemDto>>), StatusCodes.Status200OK)]
    public Task<IResult> ListAuctionProducts(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] string? status)
    {
        CatalogValidation.EnsurePaged(pagination.Page, pagination.PageSize);
        CatalogValidation.EnsureOptionalAuctionStatus(status);

        return ForwardPagedAsync(
            Service,
            "/api/v1/product/auctions",
            pagination.Page,
            pagination.PageSize,
            HttpMethod.Get,
            mockSeed: "auction-products",
            queryParams: new Dictionary<string, string?>
            {
                ["page"] = pagination.Page.ToString(),
                ["pageSize"] = pagination.PageSize.ToString(),
                ["status"] = status
            }
        );
    }

    /// <summary>Gets auction product details by auction session ID.</summary>
    [HttpGet("product/auction/{auctionId}")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<AuctionProductDetailResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> GetAuctionProduct([FromRoute] string auctionId)
    {
        CatalogValidation.EnsureAuctionId(auctionId);

        return ForwardAsync(
            Service,
            $"/api/v1/product/auction/{auctionId}",
            HttpMethod.Get,
            mockSeed: "auction-product"
        );
    }

    private static void EnsureAttributeFilter(string? attributes)
    {
        if (string.IsNullOrWhiteSpace(attributes)) return;

        foreach (var pair in attributes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = pair.Split('=', StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
                throw new GatewayInputException("Attributes must use the AttrName=AttrValue form.", "attributes", CatalogValidation.ErrorCode);
        }
    }
}
