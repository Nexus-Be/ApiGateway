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
public sealed class CategoriesController : GatewayBaseController
{
    private const string Service = "catalog";

    public CategoriesController(IGatewayDispatcher dispatcher)
        : base(dispatcher)
    {
    }

    /// <summary>Lists categories with optional tree view.</summary>
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

    /// <summary>Gets a category with its parent and children.</summary>
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

    /// <summary>Creates a new category.</summary>
    [HttpPost("categories")]
    [Authorize(GatewayPolicies.SellerOrStaffOrAdmin)]
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

    /// <summary>Updates an existing category.</summary>
    [HttpPut("categories/{categoryId}")]
    [Authorize(GatewayPolicies.StaffOrAdmin)]
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

    /// <summary>Soft deletes a category.</summary>
    [HttpDelete("categories/{categoryId}")]
    [Authorize(GatewayPolicies.StaffOrAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IResult> DeleteCategory([FromRoute] string categoryId)
    {
        CatalogValidation.EnsureCategoryId(categoryId);

        return ForwardNoContentAsync(
            Service,
            $"/api/v1/categories/{categoryId}",
            HttpMethod.Delete,
            mockSeed: "category-delete"
        );
    }
}
