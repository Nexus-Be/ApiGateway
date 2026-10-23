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
public sealed class InventoryController : ControllerBase
{
    private readonly IGatewayDispatcher _dispatcher;
    private readonly CatalogApi.CatalogApiClient _client;

    public InventoryController(IGatewayDispatcher dispatcher, CatalogApi.CatalogApiClient client)
    {
        _dispatcher = dispatcher;
        _client = client;
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
            throw new GatewayInputException("Available must be greater than or equal to 0.", "Available");

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
}
