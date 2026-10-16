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

    [HttpGet("gateway/api/v1/catalog/categories")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PagedData<Category>>), StatusCodes.Status200OK)]
    public Task<IResult> ListCategories([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (!ValidationHelpers.PageOk(page, pageSize))
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, ValidationHelpers.PageMessage, field: "Pagination"));

        return _dispatcher.DispatchPagedAsync(
            HttpContext,
            "catalog",
            "categories",
            page,
            pageSize,
            o => _client.ListCategoriesAsync(new PageRequest { Page = page, PageSize = pageSize }, o),
            CategoryPage.Parser
        );
    }

    [HttpGet("gateway/api/v1/catalog/products")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PagedData<Product>>), StatusCodes.Status200OK)]
    public Task<IResult> ListProducts(
        [FromQuery] string? categoryId = null,
        [FromQuery] string? sellerId = null,
        [FromQuery] string? q = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (!ValidationHelpers.PageOk(page, pageSize))
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, ValidationHelpers.PageMessage, field: "Pagination"));
        if (!ValidationHelpers.GuidOk(categoryId) || !ValidationHelpers.GuidOk(sellerId))
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "categoryId/sellerId phải là UUID"));

        return _dispatcher.DispatchPagedAsync(
            HttpContext,
            "catalog",
            "products",
            page,
            pageSize,
            o => _client.ListProductsAsync(new ProductQuery
            {
                Page = page,
                PageSize = pageSize,
                CategoryId = categoryId ?? "",
                SellerId = sellerId ?? "",
                Q = q ?? ""
            }, o),
            ProductPage.Parser
        );
    }

    [HttpGet("gateway/api/v1/catalog/products/{id:guid}")]
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

    [HttpPost("gateway/api/v1/catalog/categories")]
    [Authorize("admin")]
    [ProducesResponseType(typeof(ApiResponse<Category>), StatusCodes.Status200OK)]
    public Task<IResult> CreateCategory([FromBody] CategoryRequestDto body)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "name là bắt buộc", field: "Name"));
        if (!ValidationHelpers.GuidOk(body.ParentId))
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "parentId phải là UUID", field: "ParentId"));

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

    [HttpPut("gateway/api/v1/catalog/categories/{id:guid}")]
    [Authorize("admin")]
    [ProducesResponseType(typeof(ApiResponse<Category>), StatusCodes.Status200OK)]
    public Task<IResult> UpdateCategory([FromRoute] Guid id, [FromBody] CategoryRequestDto body)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "name là bắt buộc", field: "Name"));
        if (!ValidationHelpers.GuidOk(body.ParentId))
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "parentId phải là UUID", field: "ParentId"));

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

    [HttpDelete("gateway/api/v1/catalog/categories/{id:guid}")]
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

    [HttpGet("gateway/api/v1/seller/products")]
    [Authorize("seller")]
    [ProducesResponseType(typeof(ApiResponse<PagedData<Product>>), StatusCodes.Status200OK)]
    public Task<IResult> ListSellerProducts([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (!ValidationHelpers.PageOk(page, pageSize))
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, ValidationHelpers.PageMessage, field: "Pagination"));

        return _dispatcher.DispatchPagedAsync(
            HttpContext,
            "catalog",
            "seller-products",
            page,
            pageSize,
            o => _client.ListProductsBySellerAsync(new ProductQuery
            {
                Page = page,
                PageSize = pageSize,
                SellerId = HttpContext.ToUserRequest().UserId
            }, o),
            ProductPage.Parser
        );
    }

    [HttpPost("gateway/api/v1/seller/products")]
    [Authorize("seller")]
    [ProducesResponseType(typeof(ApiResponse<Product>), StatusCodes.Status200OK)]
    public Task<IResult> CreateProduct([FromBody] ProductRequestDto body)
    {
        if (!ProductValid(body, out var price, out var error))
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, error));

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

    [HttpPut("gateway/api/v1/seller/products/{id:guid}")]
    [Authorize("seller")]
    [ProducesResponseType(typeof(ApiResponse<Product>), StatusCodes.Status200OK)]
    public Task<IResult> UpdateProduct([FromRoute] Guid id, [FromBody] ProductRequestDto body)
    {
        if (!ProductValid(body, out var price, out var error))
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, error));
        if (body.Available != 0)
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "available không đổi qua cập nhật sản phẩm — dùng PUT /gateway/api/v1/seller/inventory/{productId}", field: "Available"));

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

    [HttpDelete("gateway/api/v1/seller/products/{id:guid}")]
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

    [HttpGet("gateway/api/v1/seller/inventory")]
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

    [HttpPut("gateway/api/v1/seller/inventory/{productId:guid}")]
    [Authorize("seller")]
    [ProducesResponseType(typeof(ApiResponse<Inventory>), StatusCodes.Status200OK)]
    public Task<IResult> AdjustInventory([FromRoute] Guid productId, [FromBody] AdjustInventoryRequestDto body)
    {
        if (body.Available < 0)
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "available phải >= 0", field: "Available"));

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

    private static bool ProductValid(ProductRequestDto body, out string price, out string error)
    {
        price = "";
        error = "";
        if (string.IsNullOrWhiteSpace(body.Name))
        {
            error = "name là bắt buộc";
            return false;
        }
        if (!decimal.TryParse(body.Price, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) || value <= 0)
        {
            error = "price phải là số thập phân > 0 (ví dụ \"1250000.00\")";
            return false;
        }
        if (body.Available < 0)
        {
            error = "available phải >= 0";
            return false;
        }
        if (!ValidationHelpers.GuidOk(body.CategoryId))
        {
            error = "categoryId phải là UUID";
            return false;
        }
        price = value.ToString("0.00", CultureInfo.InvariantCulture);
        return true;
    }
}
