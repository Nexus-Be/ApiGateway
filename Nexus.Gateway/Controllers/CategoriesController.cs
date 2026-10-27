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

    public CategoriesController(IGatewayDispatcher dispatcher)
        : base(dispatcher)
    {
    }

    /// <summary>Danh sách danh mục (CAT-19)</summary>
    [HttpGet("categories")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<CategoryListResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> ListCategories(
        [FromQuery] string? parentId,
        [FromQuery] bool tree = false,
        [FromQuery] string? keyword = null)
    {
        if (!string.IsNullOrWhiteSpace(parentId)) CatalogValidation.EnsureCategoryId(parentId);

        var queryParams = new Dictionary<string, string?>
        {
            ["tree"] = tree.ToString().ToLowerInvariant(),
            ["keyword"] = keyword
        };
        if (!string.IsNullOrWhiteSpace(parentId)) queryParams["parentId"] = parentId;

        return ForwardAsync(
            Service,
            "/api/v1/categories",
            HttpMethod.Get,
            mockSeed: "categories",
            queryParams: queryParams
        );
    }

    /// <summary>Xem một danh mục kèm danh mục cha và con (CAT-18)</summary>
    [HttpGet("categories/{categoryId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<CategoryDetailResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> GetCategory([FromRoute] string categoryId)
    {
        CatalogValidation.EnsureCategoryId(categoryId);

        return ForwardAsync(
            Service,
            $"/api/v1/categories/{categoryId}",
            HttpMethod.Get,
            mockSeed: "category"
        );
    }

    /// <summary>Tạo danh mục mới (CAT-15)</summary>
    [HttpPost("categories")]
    [Authorize(GatewayPolicies.Admin)]
    [ProducesResponseType(typeof(ApiResponse<CategoryCreatedResponseDto>), StatusCodes.Status201Created)]
    public Task<IResult> CreateCategory([FromBody] CreateCategoryDto body)
    {
        CatalogValidation.EnsureCreateCategory(body);

        return ForwardAsync(
            Service,
            "/api/v1/categories",
            HttpMethod.Post,
            body: body,
            mockSeed: "category-create",
            successMessage: "Category created successfully.",
            successStatusCode: StatusCodes.Status201Created
        );
    }

    /// <summary>Cập nhật danh mục (CAT-16)</summary>
    [HttpPut("categories/{categoryId}")]
    [Authorize(GatewayPolicies.Admin)]
    [ProducesResponseType(typeof(ApiResponse<CategoryUpdatedResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> UpdateCategory([FromRoute] string categoryId, [FromBody] UpdateCategoryDto body)
    {
        CatalogValidation.EnsureCategoryId(categoryId);
        CatalogValidation.EnsureUpdateCategory(body);

        return ForwardAsync(
            Service,
            $"/api/v1/categories/{categoryId}",
            HttpMethod.Put,
            body: body,
            mockSeed: "category-update"
        );
    }

    /// <summary>Xoá mềm danh mục (CAT-17)</summary>
    [HttpDelete("categories/{categoryId}")]
    [HttpDelete("categories")]
    [Authorize(GatewayPolicies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IResult> DeleteCategory([FromRoute] string? categoryId = null, [FromQuery] string? id = null)
    {
        var targetId = categoryId ?? id ?? "cat-001";
        CatalogValidation.EnsureCategoryId(targetId);

        return ForwardNoContentAsync(
            Service,
            $"/api/v1/categories/{targetId}",
            HttpMethod.Delete,
            mockSeed: "category-delete"
        );
    }
}
