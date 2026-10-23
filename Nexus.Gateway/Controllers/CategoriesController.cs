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

[ApiController]
[Route("api/v1")]
[BackendTag("catalog")]
[EnableRateLimiting("api")]
public sealed class CategoriesController : ControllerBase
{
    private readonly IGatewayDispatcher _dispatcher;
    private readonly CatalogApi.CatalogApiClient _client;

    public CategoriesController(IGatewayDispatcher dispatcher, CatalogApi.CatalogApiClient client)
    {
        _dispatcher = dispatcher;
        _client = client;
    }

    [HttpGet("catalog/categories")]
    [HttpGet("categories")]
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

    [HttpPost("catalog/categories")]
    [HttpPost("categories")]
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
    [HttpPut("categories/{id:guid}")]
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
    [HttpDelete("categories/{id:guid}")]
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
}
