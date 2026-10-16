namespace Nexus.Gateway.Controllers;

using System.Globalization;
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

[ApiController]
[Route("api/v1")]
[BackendTag("catalog")]
[EnableRateLimiting("api")]
public sealed class CatalogController : ControllerBase
{
    private readonly IGatewayDispatcher _dispatcher;
    private readonly CatalogApi.CatalogApiClient _client;

    public CatalogController(IGatewayDispatcher dispatcher, CatalogApi.CatalogApiClient client)
    {
        _dispatcher = dispatcher;
        _client = client;
    }

    // ---------- Catalog Public Endpoints ----------

    [HttpGet("catalog/categories")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PagedData<Category>>), StatusCodes.Status200OK)]
    public Task<IResult> ListCategories([FromQuery] PaginationRequest pagination)
    {
        pagination.EnsureValid();

        return _dispatcher.DispatchPagedAsync(
            HttpContext,
            "catalog",
            "categories",
            pagination.Page,
            pagination.PageSize,
            o => _client.ListCategoriesAsync(new PageRequest { Page = pagination.Page, PageSize = pagination.PageSize }, o),
            CategoryPage.Parser
        );
    }

    [HttpGet("catalog/products")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PagedData<Product>>), StatusCodes.Status200OK)]
    public Task<IResult> ListProducts(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] string? categoryId = null,
        [FromQuery] string? sellerId = null,
        [FromQuery] string? q = null)
    {
        pagination.EnsureValid();
        ValidationHelpers.EnsureGuid(categoryId, "categoryId");
        ValidationHelpers.EnsureGuid(sellerId, "sellerId");

        return _dispatcher.DispatchPagedAsync(
            HttpContext,
            "catalog",
            "products",
            pagination.Page,
            pagination.PageSize,
            o => _client.ListProductsAsync(new ProductQuery
            {
                Page = pagination.Page,
                PageSize = pagination.PageSize,
                CategoryId = categoryId ?? "",
                SellerId = sellerId ?? "",
                Q = q ?? ""
            }, o),
            ProductPage.Parser
        );
    }

    [HttpGet("catalog/products/{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<Product>), StatusCodes.Status200OK)]
    public Task<IResult> GetProduct([FromRoute] Guid id)
    {
        return _dispatcher.DispatchAsync(
            HttpContext,
            "catalog",
            "product",
            o => _client.GetProductAsync(new IdRequest { Id = id.ToString() }, o),
            Product.Parser,
            successMessage: "Product retrieved successfully."
        );
    }

    // ---------- Catalog Admin Endpoints ----------

    [HttpPost("catalog/categories")]
    [Authorize("admin")]
    [ProducesResponseType(typeof(ApiResponse<Category>), StatusCodes.Status200OK)]
    public Task<IResult> CreateCategory([FromBody] CategoryRequestDto body)
    {
        ValidationHelpers.EnsureRequired(body.Name, "Name");
        ValidationHelpers.EnsureGuid(body.ParentId, "ParentId");

        return _dispatcher.DispatchAsync(
            HttpContext,
            "catalog",
            "category-create",
            o => _client.CreateCategoryAsync(new CategoryInput
            {
                Name = body.Name,
                ParentId = body.ParentId ?? ""
            }, o),
            Category.Parser,
            successMessage: "Category created successfully."
        );
    }

    [HttpPut("catalog/categories/{id:guid}")]
    [Authorize("admin")]
    [ProducesResponseType(typeof(ApiResponse<Category>), StatusCodes.Status200OK)]
    public Task<IResult> UpdateCategory([FromRoute] Guid id, [FromBody] CategoryRequestDto body)
    {
        ValidationHelpers.EnsureRequired(body.Name, "Name");
        ValidationHelpers.EnsureGuid(body.ParentId, "ParentId");

        return _dispatcher.DispatchAsync(
            HttpContext,
            "catalog",
            "category-update",
            o => _client.UpdateCategoryAsync(new UpdateCategoryRequest
            {
                Id = id.ToString(),
                Name = body.Name,
                ParentId = body.ParentId ?? ""
            }, o),
            Category.Parser,
            successMessage: "Category updated successfully."
        );
    }

    [HttpDelete("catalog/categories/{id:guid}")]
    [Authorize("admin")]
    [ProducesResponseType(typeof(ApiResponse<DeleteResponse>), StatusCodes.Status200OK)]
    public Task<IResult> DeleteCategory([FromRoute] Guid id)
    {
        return _dispatcher.DispatchAsync(
            HttpContext,
            "catalog",
            "category-delete",
            o => _client.DeleteCategoryAsync(new IdRequest { Id = id.ToString() }, o),
            DeleteResponse.Parser,
            successMessage: "Category deleted."
        );
    }

    // ---------- Seller Catalog Endpoints ----------

    [HttpGet("seller/products")]
    [Authorize("seller")]
    [ProducesResponseType(typeof(ApiResponse<PagedData<Product>>), StatusCodes.Status200OK)]
    public Task<IResult> ListSellerProducts([FromQuery] PaginationRequest pagination)
    {
        pagination.EnsureValid();

        return _dispatcher.DispatchPagedAsync(
            HttpContext,
            "catalog",
            "seller-products",
            pagination.Page,
            pagination.PageSize,
            o => _client.ListProductsBySellerAsync(new ProductQuery
            {
                Page = pagination.Page,
                PageSize = pagination.PageSize,
                SellerId = HttpContext.ToUserRequest().UserId
            }, o),
            ProductPage.Parser
        );
    }

    [HttpPost("seller/products")]
    [Authorize("seller")]
    [ProducesResponseType(typeof(ApiResponse<Product>), StatusCodes.Status200OK)]
    public Task<IResult> CreateProduct([FromBody] ProductRequestDto body)
    {
        var price = ValidateProduct(body);

        return _dispatcher.DispatchAsync(
            HttpContext,
            "catalog",
            "product-create",
            o => _client.CreateProductAsync(new ProductInput
            {
                SellerId = HttpContext.ToUserRequest().UserId,
                Name = body.Name,
                Description = body.Description ?? "",
                CategoryId = body.CategoryId ?? "",
                Price = price,
                Currency = body.Currency ?? ValidationHelpers.DefaultCurrency,
                Available = body.Available,
                Slug = body.Slug ?? ""
            }, o),
            Product.Parser,
            successMessage: "Product created successfully."
        );
    }

    [HttpPut("seller/products/{id:guid}")]
    [Authorize("seller")]
    [ProducesResponseType(typeof(ApiResponse<Product>), StatusCodes.Status200OK)]
    public Task<IResult> UpdateProduct([FromRoute] Guid id, [FromBody] ProductRequestDto body)
    {
        var price = ValidateProduct(body);
        if (body.Available != 0)
            throw new GatewayInputException("available không đổi qua cập nhật sản phẩm — dùng PUT /api/v1/seller/inventory/{productId}", "Available");

        return _dispatcher.DispatchAsync(
            HttpContext,
            "catalog",
            "product-update",
            o => _client.UpdateProductAsync(new UpdateProductRequest
            {
                Id = id.ToString(),
                SellerId = HttpContext.ToUserRequest().UserId,
                Name = body.Name,
                Description = body.Description ?? "",
                CategoryId = body.CategoryId ?? "",
                Price = price,
                Currency = body.Currency ?? ValidationHelpers.DefaultCurrency,
                Slug = body.Slug ?? ""
            }, o),
            Product.Parser,
            successMessage: "Product updated successfully."
        );
    }

    [HttpDelete("seller/products/{id:guid}")]
    [Authorize("seller")]
    [ProducesResponseType(typeof(ApiResponse<DeleteResponse>), StatusCodes.Status200OK)]
    public Task<IResult> DeleteProduct([FromRoute] Guid id)
    {
        return _dispatcher.DispatchAsync(
            HttpContext,
            "catalog",
            "product-delete",
            o => _client.DeleteProductAsync(new IdRequest { Id = id.ToString() }, o),
            DeleteResponse.Parser,
            successMessage: "Product deleted."
        );
    }

    [HttpGet("seller/inventory")]
    [Authorize("seller")]
    [ProducesResponseType(typeof(ApiResponse<InventoryPage>), StatusCodes.Status200OK)]
    public Task<IResult> GetInventory()
    {
        return _dispatcher.DispatchAsync(
            HttpContext,
            "catalog",
            "inventory",
            o => _client.GetInventoryAsync(HttpContext.ToUserRequest(), o),
            InventoryPage.Parser,
            successMessage: "Inventory retrieved successfully."
        );
    }

    [HttpPut("seller/inventory/{productId:guid}")]
    [Authorize("seller")]
    [ProducesResponseType(typeof(ApiResponse<Inventory>), StatusCodes.Status200OK)]
    public Task<IResult> AdjustInventory([FromRoute] Guid productId, [FromBody] AdjustInventoryRequestDto body)
    {
        if (body.Available < 0)
            throw new GatewayInputException("available phải >= 0", "Available");

        return _dispatcher.DispatchAsync(
            HttpContext,
            "catalog",
            "inventory-adjust",
            o => _client.AdjustInventoryAsync(new AdjustInventoryRequest
            {
                SellerId = HttpContext.ToUserRequest().UserId,
                ProductId = productId.ToString(),
                Available = body.Available,
                Reason = body.Reason ?? ""
            }, o),
            Inventory.Parser,
            successMessage: "Inventory updated successfully."
        );
    }

    private static string ValidateProduct(ProductRequestDto body)
    {
        ValidationHelpers.EnsureRequired(body.Name, "Name");
        if (!MoneyPrecision.TryParsePositive(body.Price, out var value))
            throw new GatewayInputException("price phải là số thập phân > 0 (ví dụ \"1250000.00\")", "Price");
        if (body.Available < 0)
            throw new GatewayInputException("available phải >= 0", "Available");
        ValidationHelpers.EnsureGuid(body.CategoryId, "CategoryId");
        return value.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
