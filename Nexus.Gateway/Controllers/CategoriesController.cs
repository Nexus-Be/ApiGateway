namespace Nexus.Gateway.Controllers;

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
/// <remarks>
/// <para>Route bám đúng hợp đồng F2: <c>/api/v1/categories</c> và <c>/api/v1/categories/{categoryId}</c> — không có segment <c>/catalog</c>.</para>
/// <para>Đọc công khai; tạo/sửa/xoá yêu cầu role <c>admin</c> (policy <c>admin</c>, staff không được ghi danh mục).</para>
/// <para>Response bọc trong <c>ApiResponse</c>; xoá trả <c>204 No Content</c>.</para>
/// </remarks>
[ApiController]
[Route("api/v1")]
[BackendTag("catalog")]
[EnableRateLimiting("api")]
public sealed class CategoriesController : ControllerBase
{
    private const string Service = "catalog";

    private readonly IGatewayDispatcher _dispatcher;
    private readonly CatalogApi.CatalogApiClient _client;

    public CategoriesController(IGatewayDispatcher dispatcher, CatalogApi.CatalogApiClient client)
    {
        _dispatcher = dispatcher;
        _client = client;
    }

    /// <summary>Danh sách danh mục (CAT-19)</summary>
    /// <remarks>
    /// <para>Quyền: công khai. Query: <c>parentId</c> (lấy danh mục con), <c>tree</c> (true = dạng cây), <c>keyword</c>.</para>
    /// <para>Response: <c>data.items[]</c> (categoryId, name, level, sortOrder, children[] — children chỉ có khi tree = true).</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: parentId sai UUID</response>
    /// <response code="404">PARENT_NOT_FOUND (Real)</response>
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

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "categories",
            o => _client.ListCategoriesAsync(request, o),
            CategoryListResponse.Parser);
    }

    /// <summary>Xem một danh mục kèm danh mục cha và con (CAT-18)</summary>
    /// <remarks>
    /// <para>Quyền: công khai. Response: <c>data.category</c>, <c>data.parent</c> (null nếu là gốc), <c>data.children[]</c>.</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: categoryId sai UUID</response>
    /// <response code="404">CATEGORY_NOT_FOUND (Real)</response>
    [HttpGet("categories/{categoryId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<CategoryDetailResponse>), StatusCodes.Status200OK)]
    public Task<IResult> GetCategory([FromRoute] string categoryId)
    {
        CatalogValidation.EnsureCategoryId(categoryId);

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "category",
            o => _client.GetCategoryAsync(new CategoryIdRequest { CategoryId = categoryId }, o),
            CategoryDetailResponse.Parser);
    }

    /// <summary>Tạo danh mục mới (CAT-15)</summary>
    /// <remarks>
    /// <para>Quyền: Admin. Body: name, description, parentId (không gửi = danh mục gốc), sortOrder.</para>
    /// <para>Response 201: <c>data.categoryId</c>, <c>data.level</c>.</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: name rỗng, parentId sai UUID; CATEGORY_LEVEL_EXCEEDED (Real) khi vượt 3 cấp</response>
    /// <response code="401">UNAUTHORIZED</response>
    /// <response code="403">FORBIDDEN: người gọi không phải Admin</response>
    /// <response code="404">PARENT_NOT_FOUND (Real)</response>
    /// <response code="409">CATEGORY_NAME_DUPLICATED (Real)</response>
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

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "category-create",
            o => _client.CreateCategoryAsync(request, o),
            CategoryCreatedResponse.Parser,
            successMessage: "Category created successfully.",
            successStatusCode: StatusCodes.Status201Created);
    }

    /// <summary>Cập nhật danh mục (CAT-16)</summary>
    /// <remarks>
    /// <para>Quyền: Admin. Body: name, description, parentId, sortOrder, isActive.</para>
    /// <para>Response: categoryId, parentId, name, description, level, sortOrder, isActive, updatedAt.</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: name rỗng, parentId sai UUID; CATEGORY_LEVEL_EXCEEDED (Real)</response>
    /// <response code="401">UNAUTHORIZED</response>
    /// <response code="403">FORBIDDEN: người gọi không phải Admin</response>
    /// <response code="404">CATEGORY_NOT_FOUND / PARENT_NOT_FOUND (Real)</response>
    /// <response code="409">CATEGORY_NAME_DUPLICATED / CATEGORY_CYCLE (Real)</response>
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

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "category-update",
            o => _client.UpdateCategoryAsync(request, o),
            CategoryUpdatedResponse.Parser);
    }

    /// <summary>Xoá mềm danh mục (CAT-17)</summary>
    /// <remarks>
    /// <para>Quyền: Admin.</para>
    /// <para>Hợp đồng ghi endpoint là <c>DELETE /api/v1/categories</c> nhưng cột Request yêu cầu <c>Path: {categoryId}</c> — spec tự mâu thuẫn, chốt dùng <c>/categories/{categoryId}</c> và trả <c>204 No Content</c>.</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: categoryId sai UUID</response>
    /// <response code="401">UNAUTHORIZED</response>
    /// <response code="403">FORBIDDEN: người gọi không phải Admin</response>
    /// <response code="404">CATEGORY_NOT_FOUND (Real)</response>
    /// <response code="409">CATEGORY_HAS_PRODUCTS / CATEGORY_HAS_CHILDREN (Real)</response>
    [HttpDelete("categories/{categoryId}")]
    [Authorize(GatewayPolicies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IResult> DeleteCategory([FromRoute] string categoryId)
    {
        CatalogValidation.EnsureCategoryId(categoryId);

        return _dispatcher.DispatchNoContentAsync(
            HttpContext, Service, "category-delete",
            o => _client.DeleteCategoryAsync(new DeleteCategoryRequest
            {
                CategoryId = categoryId,
                Caller = CatalogValidation.Caller(HttpContext)
            }, o),
            DeleteResponse.Parser);
    }
}
