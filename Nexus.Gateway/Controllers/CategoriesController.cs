namespace Nexus.Gateway.Controllers;

using System;
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
using ShoppingCart.Contracts.V1;

/// <summary>
/// Category HTTP API — gateway-side slice of the F2 category flow (CAT-15 .. CAT-19).
/// </summary>
[ApiController]
[Route("api/v1")]
[BackendTag("catalog")]
[EnableRateLimiting("api")]
public sealed class CategoriesController : GatewayBaseController
{
    private const string Service = "catalog";
    private readonly CatalogApi.CatalogApiClient _client;

    public CategoriesController(IGatewayDispatcher dispatcher, CatalogApi.CatalogApiClient client)
        : base(dispatcher)
    {
        _client = client;
    }

    /// <summary>Danh sách danh mục (CAT-19)</summary>
    [HttpGet("categories")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<CategoryListResponse>), StatusCodes.Status200OK)]
    public Task<IResult> ListCategories(
        [FromQuery] string? parentId,
        [FromQuery] bool tree = false,
        [FromQuery] string? keyword = null)
    {
        if (!string.IsNullOrWhiteSpace(parentId)) CatalogValidation.EnsureCategoryId(parentId);

        var request = new ListCategoriesRequest { Tree = tree, Keyword = keyword ?? string.Empty };
        if (!string.IsNullOrWhiteSpace(parentId)) request.ParentId = parentId;

        return ForwardAsync(
            Service,
            "/api/v1/categories",
            HttpMethod.Get,
            grpcCall: o => _client.ListCategoriesAsync(request, o),
            parser: CategoryListResponse.Parser,
            mockSeed: "categories"
        );
    }

    /// <summary>Xem một danh mục kèm danh mục cha và con (CAT-18)</summary>
    [HttpGet("categories/{categoryId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<CategoryDetailResponse>), StatusCodes.Status200OK)]
    public Task<IResult> GetCategory([FromRoute] string categoryId)
    {
        CatalogValidation.EnsureCategoryId(categoryId);

        return ForwardAsync(
            Service,
            $"/api/v1/categories/{categoryId}",
            HttpMethod.Get,
            grpcCall: o => _client.GetCategoryAsync(new CategoryIdRequest { CategoryId = categoryId }, o),
            parser: CategoryDetailResponse.Parser,
            mockSeed: "category"
        );
    }

    /// <summary>Tạo danh mục mới (CAT-15)</summary>
    [HttpPost("categories")]
    [Authorize(GatewayPolicies.Admin)]
    [ProducesResponseType(typeof(ApiResponse<CategoryCreatedResponse>), StatusCodes.Status201Created)]
    public Task<IResult> CreateCategory([FromBody] CreateCategoryDto body)
    {
        CatalogValidation.EnsureCreateCategory(body);

        var request = new CreateCategoryRequest
        {
            Name = body.Name!,
            Description = body.Description ?? string.Empty,
            SortOrder = body.SortOrder,
            Caller = CatalogValidation.Caller(HttpContext)
        };
        if (body.ParentId.HasValue && body.ParentId != Guid.Empty) request.ParentId = body.ParentId.Value.ToString();

        return ForwardAsync(
            Service,
            "/api/v1/categories",
            HttpMethod.Post,
            grpcCall: o => _client.CreateCategoryAsync(request, o),
            parser: CategoryCreatedResponse.Parser,
            body: body,
            mockSeed: "category-create",
            successMessage: "Category created successfully.",
            successStatusCode: StatusCodes.Status201Created
        );
    }

    /// <summary>Cập nhật danh mục (CAT-16)</summary>
    [HttpPut("categories/{categoryId}")]
    [Authorize(GatewayPolicies.Admin)]
    [ProducesResponseType(typeof(ApiResponse<CategoryUpdatedResponse>), StatusCodes.Status200OK)]
    public Task<IResult> UpdateCategory([FromRoute] string categoryId, [FromBody] UpdateCategoryDto body)
    {
        CatalogValidation.EnsureCategoryId(categoryId);
        CatalogValidation.EnsureUpdateCategory(body);

        var request = new UpdateCategoryRequest
        {
            CategoryId = categoryId,
            Name = body.Name!,
            Description = body.Description ?? string.Empty,
            SortOrder = body.SortOrder,
            IsActive = body.IsActive,
            Caller = CatalogValidation.Caller(HttpContext)
        };
        if (body.ParentId.HasValue && body.ParentId != Guid.Empty) request.ParentId = body.ParentId.Value.ToString();

        return ForwardAsync(
            Service,
            $"/api/v1/categories/{categoryId}",
            HttpMethod.Put,
            grpcCall: o => _client.UpdateCategoryAsync(request, o),
            parser: CategoryUpdatedResponse.Parser,
            body: body,
            mockSeed: "category-update"
        );
    }

    /// <summary>Xoá mềm danh mục (CAT-17)</summary>
    [HttpDelete("categories/{categoryId}")]
    [Authorize(GatewayPolicies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IResult> DeleteCategory([FromRoute] string categoryId)
    {
        CatalogValidation.EnsureCategoryId(categoryId);

        return ForwardNoContentAsync(
            Service,
            $"/api/v1/categories/{categoryId}",
            HttpMethod.Delete,
            grpcCall: o => _client.DeleteCategoryAsync(new DeleteCategoryRequest
            {
                CategoryId = categoryId,
                Caller = CatalogValidation.Caller(HttpContext)
            }, o),
            parser: DeleteResponse.Parser,
            mockSeed: "category-delete"
        );
    }
}
