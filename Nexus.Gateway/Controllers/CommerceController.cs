namespace Nexus.Gateway.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.Gateway.Common;
using Nexus.Gateway.Dispatching;
using Nexus.Gateway.Dtos.Commerce;
using Nexus.Gateway.Responses;
using Nexus.Gateway.Security;
using ShoppingCart.Contracts.V1;

[ApiController]
[Route("api/v1")]
[BackendTag("commerce")]
[EnableRateLimiting("api")]
public sealed class CommerceController : ControllerBase
{
    private readonly IGatewayDispatcher _dispatcher;
    private readonly CommerceApi.CommerceApiClient _client;

    public CommerceController(IGatewayDispatcher dispatcher, CommerceApi.CommerceApiClient client)
    {
        _dispatcher = dispatcher;
        _client = client;
    }

    // ---------- Cart Endpoints ----------

    [HttpGet("commerce/cart")]
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
    [Authorize("authenticated")]
    [ProducesResponseType(typeof(ApiResponse<Cart>), StatusCodes.Status200OK)]
    public Task<IResult> AddCartItem([FromBody] AddCartItemRequestDto body)
    {
        ValidationHelpers.EnsureGuid(body.ProductId, "ProductId");
        if (body.Quantity < 1)
            throw new GatewayInputException("quantity phải >= 1", "Quantity");

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
    [Authorize("authenticated")]
    [ProducesResponseType(typeof(ApiResponse<Cart>), StatusCodes.Status200OK)]
    public Task<IResult> UpdateCartItem([FromRoute] Guid productId, [FromBody] UpdateCartItemRequestDto body)
    {
        if (body.Quantity < 1)
            throw new GatewayInputException("quantity phải >= 1", "Quantity");

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

    // ---------- Orders Endpoints ----------

    [HttpGet("commerce/orders")]
    [Authorize("authenticated")]
    [ProducesResponseType(typeof(ApiResponse<PagedData<Order>>), StatusCodes.Status200OK)]
    public Task<IResult> ListOrders([FromQuery] PaginationRequest pagination)
    {
        pagination.EnsureValid();

        return _dispatcher.DispatchPagedAsync(
            HttpContext,
            "commerce",
            "orders",
            pagination.Page,
            pagination.PageSize,
            o => _client.ListOrdersAsync(new MyOrdersRequest
            {
                UserId = HttpContext.ToUserRequest().UserId,
                Page = pagination.Page,
                PageSize = pagination.PageSize
            }, o),
            OrderPage.Parser
        );
    }

    [HttpGet("commerce/orders/{id:guid}")]
    [Authorize("authenticated")]
    [ProducesResponseType(typeof(ApiResponse<Order>), StatusCodes.Status200OK)]
    public Task<IResult> GetOrder([FromRoute] Guid id)
    {
        return _dispatcher.DispatchAsync(
            HttpContext,
            "commerce",
            "order",
            o => _client.GetOrderAsync(new OrderRequest
            {
                OrderId = id.ToString(),
                UserId = HttpContext.ToUserRequest().UserId,
                Role = HttpContext.GetPrimaryRole()
            }, o),
            Order.Parser,
            successMessage: "Order retrieved successfully."
        );
    }

    [HttpPost("commerce/orders")]
    [Authorize("authenticated")]
    [ProducesResponseType(typeof(ApiResponse<Order>), StatusCodes.Status201Created)]
    public Task<IResult> CreateOrder([FromBody] CheckoutRequestDto body)
    {
        var key = HttpContext.Request.Headers["Idempotency-Key"].ToString();
        if (!Guid.TryParse(key, out _))
            throw new GatewayInputException("Idempotency-Key phải là UUID", "Idempotency-Key");

        var lines = body.Items ?? Array.Empty<CheckoutLineDto>();
        if (lines.Count == 0)
        {
            if (!Guid.TryParse(body.CartId, out _))
                throw new GatewayInputException("cần 'items' (mua ngay) hoặc 'cartId' (checkout cả giỏ)");
        }
        else
        {
            if (lines.Count > ValidationHelpers.MaxCheckoutLines)
                throw new GatewayInputException($"tối đa {ValidationHelpers.MaxCheckoutLines} dòng hàng mỗi đơn");

            foreach (var line in lines)
            {
                ValidationHelpers.EnsureGuid(line.ProductId, "ProductId");
                if (line.Quantity < 1 || line.Quantity > ValidationHelpers.MaxCheckoutQuantity)
                    throw new GatewayInputException($"quantity phải trong khoảng 1..{ValidationHelpers.MaxCheckoutQuantity}", "Quantity");
            }
        }

        var request = new CreateOrderRequest
        {
            UserId = HttpContext.ToUserRequest().UserId,
            CartId = body.CartId ?? string.Empty,
            IdempotencyKey = key,
        };
        request.Items.AddRange(lines.Select(l => new CreateOrderLine { ProductId = l.ProductId, Quantity = l.Quantity }));

        return _dispatcher.DispatchAsync(
            HttpContext,
            "commerce",
            "order-created",
            o => _client.CreateOrderAsync(request, o),
            Order.Parser,
            successMessage: "Order created successfully.",
            successStatusCode: StatusCodes.Status201Created
        );
    }

    [HttpGet("seller/orders")]
    [Authorize("seller")]
    [ProducesResponseType(typeof(ApiResponse<PagedData<Order>>), StatusCodes.Status200OK)]
    public Task<IResult> ListSellerOrders([FromQuery] PaginationRequest pagination)
    {
        pagination.EnsureValid();

        return _dispatcher.DispatchPagedAsync(
            HttpContext,
            "commerce",
            "seller-orders",
            pagination.Page,
            pagination.PageSize,
            o => _client.ListOrdersBySellerAsync(new SellerOrdersRequest
            {
                SellerId = HttpContext.ToUserRequest().UserId,
                Page = pagination.Page,
                PageSize = pagination.PageSize
            }, o),
            OrderPage.Parser
        );
    }

    [HttpGet("admin/orders")]
    [Authorize("staff")]
    [ProducesResponseType(typeof(ApiResponse<PagedData<Order>>), StatusCodes.Status200OK)]
    public Task<IResult> ListAdminOrders([FromQuery] PaginationRequest pagination)
    {
        pagination.EnsureValid();

        return _dispatcher.DispatchPagedAsync(
            HttpContext,
            "commerce",
            "admin-orders",
            pagination.Page,
            pagination.PageSize,
            o => _client.ListAllOrdersAsync(new PageRequest
            {
                Page = pagination.Page,
                PageSize = pagination.PageSize
            }, o),
            OrderPage.Parser
        );
    }
}
