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
/// F1 checkout and order surface (CHK-01, ORD-01 .. ORD-05). Routes follow the F1 contract sheet:
/// <c>/api/v1/checkout/*</c>, <c>/api/v1/orders/*</c>, <c>/api/v1/order/{orderId}</c>.
/// </summary>
/// <remarks>
/// <para>Mock đọc <c>Seeds/commerce/*.json</c>; response bọc trong <c>ApiResponse&lt;T&gt;</c>.</para>
/// <para>Real chưa nối — xem <see cref="CartController"/>.</para>
/// </remarks>
[ApiController]
[Route("api/v1")]
[BackendTag("commerce")]
[EnableRateLimiting("api")]
public sealed class OrdersController : ControllerBase
{
    private const string Service = "commerce";

    private static readonly MessageParser<Struct> SeedParser = Struct.Parser;

    private readonly IGatewayDispatcher _dispatcher;

    public OrdersController(IGatewayDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    private static AsyncUnaryCall<Struct> PendingRealCall(CallOptions _) =>
        throw new NotSupportedException("The commerce gRPC contract is not wired yet.");

    /// <summary>Tính trước tiền hàng, giảm giá và phí ship (CHK-01)</summary>
    [HttpPost("checkout/quote")]
    [Authorize(Policy = GatewayPolicies.Customer)]
    public Task<IResult> CreateCheckoutQuote([FromBody] CheckoutQuoteRequest body)
    {
        if (body.CartItemIds is null || body.CartItemIds.Count == 0)
            throw new GatewayInputException("No valid items selected for checkout.", "CartItemIds");
        if (body.ShippingAddress is null
            || string.IsNullOrWhiteSpace(body.ShippingAddress.ReceiverName)
            || string.IsNullOrWhiteSpace(body.ShippingAddress.Street)
            || string.IsNullOrWhiteSpace(body.ShippingAddress.District)
            || string.IsNullOrWhiteSpace(body.ShippingAddress.Province))
            throw new GatewayInputException("Shipping address is incomplete.", "ShippingAddress");
        if (string.IsNullOrWhiteSpace(body.ContactPhone))
            throw new GatewayInputException("Contact phone number is required.", "ContactPhone");

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "checkout-quote", PendingRealCall, SeedParser);
    }

    /// <summary>Tạo đơn hàng từ quote đã tính (ORD-01)</summary>
    [HttpPost("orders")]
    [Authorize(Policy = GatewayPolicies.Customer)]
    public Task<IResult> CreateOrder([FromBody] CreateOrderRequest body)
    {
        if (string.IsNullOrWhiteSpace(body.QuoteId))
            throw new GatewayInputException("Checkout quote ID is required.", "QuoteId");
        if (string.IsNullOrWhiteSpace(body.ShippingAddress) || string.IsNullOrWhiteSpace(body.ContactPhone))
            throw new GatewayInputException("Shipping address and phone number are required.", "ShippingAddress");

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "order-create", PendingRealCall, SeedParser,
            successMessage: "Order created successfully.",
            successStatusCode: StatusCodes.Status201Created);
    }

    /// <summary>Xem chi tiết một đơn hàng (ORD-02)</summary>
    [HttpGet("order/{orderId}")]
    [Authorize(Policy = GatewayPolicies.Customer)]
    public Task<IResult> GetOrder(string orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            throw new GatewayInputException("Order ID is required.", "OrderId");

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "order-get", PendingRealCall, SeedParser);
    }

    /// <summary>Xem chi tiết một đơn con theo người bán (ORD-03)</summary>
    [HttpGet("subOrder/{subOrderId}")]
    [Authorize(Policy = GatewayPolicies.Customer)]
    public Task<IResult> GetSubOrder(string subOrderId)
    {
        if (string.IsNullOrWhiteSpace(subOrderId))
            throw new GatewayInputException("Sub-order ID is required.", "SubOrderId");

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "sub-order-get", PendingRealCall, SeedParser);
    }

    /// <summary>Người mua huỷ đơn khi chưa thanh toán (ORD-06)</summary>
    [HttpPost("orders/{orderId}/cancel")]
    [Authorize(Policy = GatewayPolicies.Customer)]
    public Task<IResult> CancelOrder(string orderId, [FromBody] CancelOrderRequest body)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            throw new GatewayInputException("Order ID is required.", "OrderId");

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "order-cancel", PendingRealCall, SeedParser,
            successMessage: "Order has been successfully cancelled.");
    }

    /// <summary>Người bán chấp nhận đơn con (ORD-04)</summary>
    [HttpPost("orders/{orderId}/accept")]
    [Authorize(Policy = GatewayPolicies.Seller)]
    public Task<IResult> AcceptSubOrder(string orderId, [FromBody] SubOrderDecisionRequest body)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            throw new GatewayInputException("Order ID is required.", "OrderId");
        if (string.IsNullOrWhiteSpace(body.SubOrderId))
            throw new GatewayInputException("Sub-order ID is required.", "SubOrderId");

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "sub-order-accept", PendingRealCall, SeedParser);
    }

    /// <summary>Người bán từ chối đơn con (ORD-05)</summary>
    [HttpPost("orders/{orderId}/reject")]
    [Authorize(Policy = GatewayPolicies.Seller)]
    public Task<IResult> RejectSubOrder(string orderId, [FromBody] SubOrderDecisionRequest body)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            throw new GatewayInputException("Order ID is required.", "OrderId");
        if (string.IsNullOrWhiteSpace(body.SubOrderId))
            throw new GatewayInputException("Sub-order ID is required.", "SubOrderId");
        if (string.IsNullOrWhiteSpace(body.ReasonCode))
            throw new GatewayInputException("Rejection reason code is required.", "ReasonCode");

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "sub-order-reject", PendingRealCall, SeedParser);
    }
}
