namespace Nexus.Gateway.Controllers;

using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.Gateway.Common;
using Nexus.Gateway.Dispatching;
using Nexus.Gateway.Dtos.Commerce;
using Nexus.Gateway.Responses;
using Nexus.Gateway.Security;

[ApiController]
[Route("api/v1")]
[BackendTag("commerce")]
[EnableRateLimiting("api")]
public sealed class CartController : GatewayBaseController
{
    private const string Service = "commerce";

    public CartController(IGatewayDispatcher dispatcher) : base(dispatcher)
    {
    }

    /// <summary>Gets current user shopping cart.</summary>
    [HttpGet("cart")]
    [Authorize(GatewayPolicies.Customer)]
    [ProducesResponseType(typeof(ApiResponse<CartResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> GetCart()
    {
        return ForwardAsync(Service, "/api/v1/cart", HttpMethod.Get, mockSeed: "cart-get");
    }

    /// <summary>Adds items to shopping cart.</summary>
    [HttpPost("cart/items")]
    [Authorize(GatewayPolicies.Customer)]
    [ProducesResponseType(typeof(ApiResponse<AddCartItemResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> AddCartItem([FromBody] AddCartItemRequest body)
    {
        if (string.IsNullOrWhiteSpace(body.ProductId) && string.IsNullOrWhiteSpace(body.SkuId))
            throw new GatewayInputException("Product ID or SKU ID is required.", "ProductId");
        if (body.Quantity <= 0)
            throw new GatewayInputException("Quantity must be greater than zero.", "Quantity");

        return ForwardAsync(Service, "/api/v1/cart/items", HttpMethod.Post, body: body, mockSeed: "cart-item-add");
    }

    /// <summary>Updates item quantity in shopping cart.</summary>
    [HttpPatch("cart/items/{cartItemId}")]
    [Authorize(GatewayPolicies.Customer)]
    [ProducesResponseType(typeof(ApiResponse<UpdateCartItemResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> UpdateCartItem([FromRoute] string cartItemId, [FromBody] UpdateCartItemRequest body)
    {
        if (string.IsNullOrWhiteSpace(cartItemId))
            throw new GatewayInputException("Cart item ID is required.", "CartItemId");
        if (body.Quantity <= 0)
            throw new GatewayInputException("Quantity must be greater than zero.", "Quantity");

        return ForwardAsync(Service, $"/api/v1/cart/items/{cartItemId}", HttpMethod.Patch, body: body, mockSeed: "cart-item-update");
    }

    /// <summary>Removes one or more items from shopping cart.</summary>
    [HttpDelete("cart/items")]
    [Authorize(GatewayPolicies.Customer)]
    [ProducesResponseType(typeof(ApiResponse<RemoveCartItemsResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> RemoveCartItems([FromBody] RemoveCartItemsRequest body)
    {
        if (!body.All && (body.CartItemIds is null || body.CartItemIds.Count == 0))
            throw new GatewayInputException("Cart item IDs must be provided.", "CartItemIds");

        return ForwardAsync(Service, "/api/v1/cart/items", HttpMethod.Delete, body: body, mockSeed: "cart-item-remove");
    }

    /// <summary>Clears all items from shopping cart.</summary>
    [HttpDelete("cart/items/all")]
    [Authorize(GatewayPolicies.Customer)]
    [ProducesResponseType(typeof(ApiResponse<ClearCartResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> ClearCart()
    {
        return ForwardAsync(Service, "/api/v1/cart/items/all", HttpMethod.Delete, mockSeed: "cart-clear");
    }

    /// <summary>Validates selected cart items prior to checkout.</summary>
    [HttpGet("cart/items/select")]
    [Authorize(GatewayPolicies.Customer)]
    [ProducesResponseType(typeof(ApiResponse<List<CartItemValidationDto>>), StatusCodes.Status200OK)]
    public Task<IResult> SelectCartItems([FromQuery(Name = "cartItemIds")] List<string>? cartItemIds)
    {
        if (cartItemIds is null || cartItemIds.Count == 0)
            throw new GatewayInputException("Cart item IDs are required.", "CartItemIds");

        var queryParams = new Dictionary<string, string?>
        {
            ["cartItemIds"] = string.Join(",", cartItemIds)
        };

        return ForwardAsync(Service, "/api/v1/cart/items/select", HttpMethod.Get, mockSeed: "cart-items-select", queryParams: queryParams);
    }
}
