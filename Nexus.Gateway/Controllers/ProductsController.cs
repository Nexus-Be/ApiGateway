namespace Nexus.Gateway.Controllers;

using System;
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
using ShoppingCart.Contracts.V1;

/// <summary>
/// Catalog HTTP API (Products, Categories, Inventory).
/// Flow Catalog được phụ trách bởi Minh, mock data đọc từ Seeds/catalog/*.json
/// </summary>
[ApiController]
[Route("api/v1/catalog")]
[BackendTag("catalog")]
[EnableRateLimiting("api")]
public sealed class ProductsController : ControllerBase
{
    private const string Service = "catalog";

    private readonly IGatewayDispatcher _dispatcher;
    private readonly CatalogApi.CatalogApiClient _client;

    public ProductsController(IGatewayDispatcher dispatcher, CatalogApi.CatalogApiClient client)
    {
        _dispatcher = dispatcher;
        _client = client;
    }

    // ==========================================
    // 1. PRODUCTS
    // ==========================================

    /// <summary>Xem danh sách sản phẩm phân trang (hỗ trợ lọc theo category)</summary>
    [HttpGet("products")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PagedData<ProductItem>>), StatusCodes.Status200OK)]
    public Task<IResult> ListProducts([FromQuery] PaginationRequest pagination, [FromQuery] string? categoryId = null)
    {
        pagination.EnsureValid();
        if (!string.IsNullOrEmpty(categoryId) && !Guid.TryParse(categoryId, out _))
            throw new GatewayInputException("Invalid categoryId UUID format.");

        return _dispatcher.DispatchPagedAsync(
            HttpContext, Service, "products",
            pagination.Page, pagination.PageSize,
            o => _client.ListProductsAsync(new ProductQueryRequest
            {
                Page = pagination.Page,
                PageSize = pagination.PageSize,
                CategoryId = categoryId
            }, o),
            ProductListResponse.Parser);
    }

    /// <summary>Xem chi tiết 1 sản phẩm theo ID</summary>
    [HttpGet("products/{id}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<ProductResponse>), StatusCodes.Status200OK)]
    public Task<IResult> GetProduct([FromRoute] string id)
    {
        if (!Guid.TryParse(id, out _))
            throw new GatewayInputException("Invalid product id UUID format.");

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "product",
            o => _client.GetProductAsync(new ProductIdRequest { Id = id }, o),
            ProductResponse.Parser,
            successMessage: "Product retrieved successfully.");
    }

    /// <summary>Xem danh sách sản phẩm của một Seller</summary>
    [HttpGet("products/seller/{sellerId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PagedData<ProductItem>>), StatusCodes.Status200OK)]
    public Task<IResult> ListProductsBySeller([FromRoute] string sellerId, [FromQuery] PaginationRequest pagination)
    {
        pagination.EnsureValid();
        if (!Guid.TryParse(sellerId, out _))
            throw new GatewayInputException("Invalid sellerId UUID format.");

        return _dispatcher.DispatchPagedAsync(
            HttpContext, Service, "seller-products",
            pagination.Page, pagination.PageSize,
            o => _client.ListProductsBySellerAsync(new SellerProductQueryRequest
            {
                SellerId = sellerId,
                Page = pagination.Page,
                PageSize = pagination.PageSize
            }, o),
            ProductListResponse.Parser);
    }

    /// <summary>Đăng bán sản phẩm mới (Dành cho Seller)</summary>
    [HttpPost("products")]
    [Authorize(Roles = "seller,admin")]
    [ProducesResponseType(typeof(ApiResponse<ProductResponse>), StatusCodes.Status201Created)]
    public Task<IResult> CreateProduct([FromBody] CreateProductDto body)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            throw new GatewayInputException("Product name is required.");
        if (body.Price < 0)
            throw new GatewayInputException("Product price must be greater than or equal to 0.");

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "product",
            o => _client.CreateProductAsync(new CreateProductProtoRequest
            {
                SellerId = body.SellerId.ToString(),
                CategoryId = body.CategoryId.ToString(),
                Name = body.Name.Trim(),
                Description = body.Description ?? "",
                Brand = body.Brand ?? "",
                Price = (double)body.Price,
                Currency = body.Currency ?? "VND"
            }, o),
            ProductResponse.Parser,
            successMessage: "Product created successfully.",
            successStatusCode: StatusCodes.Status201Created);
    }

    /// <summary>Cập nhật thông tin sản phẩm</summary>
    [HttpPut("products/{id}")]
    [Authorize(Roles = "seller,admin")]
    [ProducesResponseType(typeof(ApiResponse<ProductResponse>), StatusCodes.Status200OK)]
    public Task<IResult> UpdateProduct([FromRoute] string id, [FromBody] UpdateProductDto body)
    {
        if (!Guid.TryParse(id, out _))
            throw new GatewayInputException("Invalid product id UUID format.");
        if (string.IsNullOrWhiteSpace(body.Name))
            throw new GatewayInputException("Product name is required.");

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "product",
            o => _client.UpdateProductAsync(new UpdateProductProtoRequest
            {
                Id = id,
                Name = body.Name.Trim(),
                Description = body.Description ?? "",
                Brand = body.Brand ?? "",
                Price = (double)body.Price,
                Status = body.Status
            }, o),
            ProductResponse.Parser,
            successMessage: "Product updated successfully.");
    }

    /// <summary>Xóa sản phẩm</summary>
    [HttpDelete("products/{id}")]
    [Authorize(Roles = "seller,admin")]
    [ProducesResponseType(typeof(ApiResponse<CatalogDeleteResponse>), StatusCodes.Status200OK)]
    public Task<IResult> DeleteProduct([FromRoute] string id)
    {
        if (!Guid.TryParse(id, out _))
            throw new GatewayInputException("Invalid product id UUID format.");

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "product-delete",
            o => _client.DeleteProductAsync(new ProductIdRequest { Id = id }, o),
            CatalogDeleteResponse.Parser,
            successMessage: "Product deleted successfully.");
    }

    // ==========================================
    // 2. CATEGORIES
    // ==========================================

    /// <summary>Xem toàn bộ danh mục sản phẩm</summary>
    [HttpGet("categories")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<CategoryListResponse>), StatusCodes.Status200OK)]
    public Task<IResult> ListCategories()
    {
        return _dispatcher.DispatchAsync(
            HttpContext, Service, "categories",
            o => _client.ListCategoriesAsync(new CategoryPageRequest { Page = 1, PageSize = 100 }, o),
            CategoryListResponse.Parser,
            successMessage: "Categories retrieved successfully.");
    }

    /// <summary>Xem chi tiết 1 danh mục</summary>
    [HttpGet("categories/{id}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<CategoryResponse>), StatusCodes.Status200OK)]
    public Task<IResult> GetCategory([FromRoute] string id)
    {
        if (!Guid.TryParse(id, out _))
            throw new GatewayInputException("Invalid category id UUID format.");

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "category",
            o => _client.GetCategoryAsync(new CategoryIdRequest { Id = id }, o),
            CategoryResponse.Parser,
            successMessage: "Category retrieved successfully.");
    }

    /// <summary>Tạo mới danh mục (Admin/Staff)</summary>
    [HttpPost("categories")]
    [Authorize(Roles = "admin,staff")]
    [ProducesResponseType(typeof(ApiResponse<CategoryResponse>), StatusCodes.Status201Created)]
    public Task<IResult> CreateCategory([FromBody] CreateCategoryDto body)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            throw new GatewayInputException("Category name is required.");

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "category",
            o => _client.CreateCategoryAsync(new CreateCategoryProtoRequest
            {
                Name = body.Name.Trim(),
                Description = body.Description ?? "",
                ParentId = body.ParentId?.ToString(),
                Level = body.Level,
                SortOrder = body.SortOrder
            }, o),
            CategoryResponse.Parser,
            successMessage: "Category created successfully.",
            successStatusCode: StatusCodes.Status201Created);
    }

    /// <summary>Cập nhật danh mục (Admin/Staff)</summary>
    [HttpPut("categories/{id}")]
    [Authorize(Roles = "admin,staff")]
    [ProducesResponseType(typeof(ApiResponse<CategoryResponse>), StatusCodes.Status200OK)]
    public Task<IResult> UpdateCategory([FromRoute] string id, [FromBody] UpdateCategoryDto body)
    {
        if (!Guid.TryParse(id, out _))
            throw new GatewayInputException("Invalid category id UUID format.");

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "category",
            o => _client.UpdateCategoryAsync(new UpdateCategoryProtoRequest
            {
                Id = id,
                Name = body.Name.Trim(),
                Description = body.Description ?? "",
                Level = body.Level,
                SortOrder = body.SortOrder,
                IsDisabled = body.IsDisabled
            }, o),
            CategoryResponse.Parser,
            successMessage: "Category updated successfully.");
    }

    /// <summary>Xóa danh mục (Admin)</summary>
    [HttpDelete("categories/{id}")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(ApiResponse<CatalogDeleteResponse>), StatusCodes.Status200OK)]
    public Task<IResult> DeleteCategory([FromRoute] string id)
    {
        if (!Guid.TryParse(id, out _))
            throw new GatewayInputException("Invalid category id UUID format.");

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "category-delete",
            o => _client.DeleteCategoryAsync(new CategoryIdRequest { Id = id }, o),
            CatalogDeleteResponse.Parser,
            successMessage: "Category deleted successfully.");
    }

    // ==========================================
    // 3. INVENTORY
    // ==========================================

    /// <summary>Xem số lượng tồn kho ảo của sản phẩm</summary>
    [HttpGet("inventory/{productId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<InventoryResponse>), StatusCodes.Status200OK)]
    public Task<IResult> GetInventory([FromRoute] string productId)
    {
        if (!Guid.TryParse(productId, out _))
            throw new GatewayInputException("Invalid productId UUID format.");

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "inventory",
            o => _client.GetInventoryAsync(new InventoryQueryRequest { ProductId = productId }, o),
            InventoryResponse.Parser,
            successMessage: "Inventory retrieved successfully.");
    }

    /// <summary>Điều chỉnh số lượng tồn kho ảo (Seller/Admin)</summary>
    [HttpPost("inventory/{productId}/adjust")]
    [Authorize(Roles = "seller,admin")]
    [ProducesResponseType(typeof(ApiResponse<InventoryResponse>), StatusCodes.Status200OK)]
    public Task<IResult> AdjustInventory([FromRoute] string productId, [FromBody] AdjustInventoryDto body)
    {
        if (!Guid.TryParse(productId, out _))
            throw new GatewayInputException("Invalid productId UUID format.");

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "inventory",
            o => _client.AdjustInventoryAsync(new AdjustInventoryProtoRequest
            {
                ProductId = productId,
                QuantityChange = body.QuantityChange
            }, o),
            InventoryResponse.Parser,
            successMessage: "Inventory adjusted successfully.");
    }
}
