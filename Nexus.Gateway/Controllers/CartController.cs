namespace Nexus.Gateway.Controllers;

using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.Gateway.Common;
using Nexus.Gateway.Dispatching;
using Nexus.Gateway.Dtos.Commerce;
using Nexus.Gateway.Security;

/// <summary>
/// F1 cart surface (CART-01 .. CART-06). Routes follow the F1 contract sheet:
/// <c>/api/v1/cart</c> and <c>/api/v1/cart/items</c> — no <c>/commerce</c> segment.
/// </summary>
/// <remarks>
/// <para>Mock đọc <c>Seeds/commerce/*.json</c> qua <c>IGatewayDispatcher</c>; response bọc trong
/// <c>ApiResponse&lt;T&gt;</c> và có header <c>X-Gateway-Mode</c>.</para>
/// <para>Real chưa nối: contract gRPC của Commerce chưa tồn tại và <c>shop.proto</c> không được sửa,
/// nên <c>realCall</c> chỉ là placeholder không bao giờ được gọi trong Mock mode.</para>
/// </remarks>
[ApiController]
[Route("api/v1")]
[BackendTag("commerce")]
[EnableRateLimiting("api")]
public sealed class CartController : ControllerBase
{
    private const string Service = "commerce";

    // Mock-only pass-through: in Mock mode GatewayDispatcher never invokes realCall, only the
    // parser. Struct round-trips the seed JSON object, so no commerce.proto is needed for Q2.
    private static readonly MessageParser<Struct> SeedParser = Struct.Parser;

    private readonly IGatewayDispatcher _dispatcher;

    public CartController(IGatewayDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    private static AsyncUnaryCall<Struct> PendingRealCall(CallOptions _) =>
        throw new NotSupportedException("The commerce gRPC contract is not wired yet.");

    /// <summary>Lấy giỏ hàng hiện tại của người dùng (CART-01)</summary>
    [HttpGet("cart")]
    [Authorize(Policy = GatewayPolicies.Customer)]
    public Task<IResult> GetCart()
    {
        return _dispatcher.DispatchAsync(
            HttpContext, Service, "cart-get", PendingRealCall, SeedParser);
    }

    /// <summary>Thêm một hoặc nhiều sản phẩm vào giỏ hàng (CART-02)</summary>
    [HttpPost("cart/items")]
    [Authorize(Policy = GatewayPolicies.Customer)]
    public Task<IResult> AddCartItem([FromBody] AddCartItemRequest body)
    {
        if (string.IsNullOrWhiteSpace(body.ProductId) && string.IsNullOrWhiteSpace(body.SkuId))
            throw new GatewayInputException("Product ID or SKU ID is required.", "ProductId");
        if (body.Quantity <= 0)
            throw new GatewayInputException("Quantity must be greater than zero.", "Quantity");

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "cart-item-add", PendingRealCall, SeedParser);
    }

    /// <summary>Đổi số lượng của một dòng trong giỏ hàng (CART-03)</summary>
    [HttpPatch("cart/items/{cartItemId}")]
    [Authorize(Policy = GatewayPolicies.Customer)]
    public Task<IResult> UpdateCartItem(string cartItemId, [FromBody] UpdateCartItemRequest body)
    {
        if (string.IsNullOrWhiteSpace(cartItemId))
            throw new GatewayInputException("Cart item ID is required.", "CartItemId");
        if (body.Quantity <= 0)
            throw new GatewayInputException("Quantity must be greater than zero.", "Quantity");

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "cart-item-update", PendingRealCall, SeedParser);
    }

    /// <summary>Xoá một hoặc nhiều dòng khỏi giỏ hàng (CART-04)</summary>
    [HttpDelete("cart/items")]
    [Authorize(Policy = GatewayPolicies.Customer)]
    public Task<IResult> RemoveCartItems([FromBody] RemoveCartItemsRequest body)
    {
        if (!body.All && (body.CartItemIds is null || body.CartItemIds.Count == 0))
            throw new GatewayInputException("Cart item IDs must be provided.", "CartItemIds");

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "cart-item-remove", PendingRealCall, SeedParser);
    }

    /// <summary>Xoá toàn bộ giỏ hàng (CART-05)</summary>
    [HttpDelete("cart/items/all")]
    [Authorize(Policy = GatewayPolicies.Customer)]
    public Task<IResult> ClearCart()
    {
        return _dispatcher.DispatchAsync(
            HttpContext, Service, "cart-clear", PendingRealCall, SeedParser);
    }

    /// <summary>Kiểm tra tính hợp lệ của các dòng được chọn trước khi checkout (CART-06)</summary>
    [HttpGet("cart/items/select")]
    [Authorize(Policy = GatewayPolicies.Customer)]
    public Task<IResult> SelectCartItems([FromQuery(Name = "cartItemIds")] List<string>? cartItemIds)
    {
        if (cartItemIds is null || cartItemIds.Count == 0)
            throw new GatewayInputException("Cart item IDs are required.", "CartItemIds");

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "cart-items-select", PendingRealCall, SeedParser);
    }
}
