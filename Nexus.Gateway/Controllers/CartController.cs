namespace Nexus.Gateway.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.Gateway.Common;
using Nexus.Gateway.Dispatching;
using Nexus.Gateway.Dtos.Cart;
using Nexus.Gateway.Responses;
using Nexus.Gateway.Security;
using ShoppingCart.Contracts.V1;

[ApiController]
[Route("api/v1")]
[BackendTag("commerce")]
[EnableRateLimiting("api")]
public sealed class CartController : ControllerBase
{
    private readonly IGatewayDispatcher _dispatcher;
    private readonly CommerceApi.CommerceApiClient _client;

    public CartController(IGatewayDispatcher dispatcher, CommerceApi.CommerceApiClient client)
    {
        _dispatcher = dispatcher;
        _client = client;
    }

    [HttpGet("commerce/cart")]
    [HttpGet("cart")]
    [Authorize("authenticated")]
    [ProducesResponseType(typeof(ApiResponse<Cart>), StatusCodes.Status200OK)]
    public Task<IResult> GetCart()
    {
        return _dispatcher.DispatchAsync(
            HttpContext,
            "commerce",
            "cart",
            o => _client.GetCartAsync(HttpContext.ToUserRequest(), o),
            Cart.Parser,
            successMessage: "Cart retrieved successfully."
        );
    }

    [HttpPost("commerce/cart/items")]
    [HttpPost("cart/items")]
    [Authorize("authenticated")]
    [ProducesResponseType(typeof(ApiResponse<Cart>), StatusCodes.Status200OK)]
    public Task<IResult> AddCartItem([FromBody] AddCartItemRequestDto body)
    {
        ValidationHelpers.EnsureGuid(body.ProductId, "ProductId");
        if (body.Quantity < 1)
            throw new GatewayInputException("Quantity must be greater than or equal to 1.", "Quantity");

        return _dispatcher.DispatchAsync(
            HttpContext,
            "commerce",
            "cart-item-add",
            o => _client.AddCartItemAsync(new CartItemRequest
            {
                UserId = HttpContext.ToUserRequest().UserId,
                ProductId = body.ProductId,
                Quantity = body.Quantity
            }, o),
            Cart.Parser,
            successMessage: "Item added to cart."
        );
    }

    [HttpPatch("commerce/cart/items/{productId:guid}")]
    [HttpPatch("cart/items/{productId:guid}")]
    [Authorize("authenticated")]
    [ProducesResponseType(typeof(ApiResponse<Cart>), StatusCodes.Status200OK)]
    public Task<IResult> UpdateCartItem([FromRoute] Guid productId, [FromBody] UpdateCartItemRequestDto body)
    {
        if (body.Quantity < 1)
            throw new GatewayInputException("Quantity must be greater than or equal to 1.", "Quantity");

        return _dispatcher.DispatchAsync(
            HttpContext,
            "commerce",
            "cart-item-update",
            o => _client.UpdateCartItemAsync(new UpdateCartItemRequest
            {
                UserId = HttpContext.ToUserRequest().UserId,
                ProductId = productId.ToString(),
                Quantity = body.Quantity
            }, o),
            Cart.Parser,
            successMessage: "Cart item updated."
        );
    }

    [HttpDelete("commerce/cart/items/{productId:guid}")]
    [HttpDelete("cart/items/{productId:guid}")]
    [Authorize("authenticated")]
    [ProducesResponseType(typeof(ApiResponse<Cart>), StatusCodes.Status200OK)]
    public Task<IResult> RemoveCartItem([FromRoute] Guid productId)
    {
        return _dispatcher.DispatchAsync(
            HttpContext,
            "commerce",
            "cart-item-remove",
            o => _client.RemoveCartItemAsync(new RemoveCartItemRequest
            {
                UserId = HttpContext.ToUserRequest().UserId,
                ProductId = productId.ToString()
            }, o),
            Cart.Parser,
            successMessage: "Item removed from cart."
        );
    }

    [HttpDelete("commerce/cart")]
    [HttpDelete("cart")]
    [Authorize("authenticated")]
    [ProducesResponseType(typeof(ApiResponse<Cart>), StatusCodes.Status200OK)]
    public Task<IResult> ClearCart()
    {
        return _dispatcher.DispatchAsync(
            HttpContext,
            "commerce",
            "cart-clear",
            o => _client.ClearCartAsync(HttpContext.ToUserRequest(), o),
            Cart.Parser,
            successMessage: "Cart cleared successfully."
        );
    }
}
