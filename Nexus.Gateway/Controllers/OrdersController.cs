namespace Nexus.Gateway.Controllers;

using System.Net.Http;
using System.Threading.Tasks;
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
[ApiController]
[Route("api/v1")]
[BackendTag("commerce")]
[EnableRateLimiting("api")]
public sealed class OrdersController : GatewayBaseController
{
    private const string Service = "commerce";

    public OrdersController(IGatewayDispatcher dispatcher) : base(dispatcher)
    {
    }

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

        return ForwardAsync(
            Service,
            "/api/v1/checkout/quote",
            HttpMethod.Post,
            body: body,
            mockSeed: "checkout-quote"
        );
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

        return ForwardAsync(
            Service,
            "/api/v1/orders",
            HttpMethod.Post,
            body: body,
            mockSeed: "order-create",
            successMessage: "Order created successfully.",
            successStatusCode: StatusCodes.Status201Created
        );
    }

    /// <summary>Xem chi tiết một đơn hàng (ORD-02)</summary>
    [HttpGet("order/{orderId}")]
    [Authorize(Policy = GatewayPolicies.Customer)]
    public Task<IResult> GetOrder(string orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            throw new GatewayInputException("Order ID is required.", "OrderId");

        return ForwardAsync(
            Service,
            $"/api/v1/order/{orderId}",
            HttpMethod.Get,
            mockSeed: "order-get"
        );
    }

    /// <summary>Xem chi tiết một đơn con theo người bán (ORD-03)</summary>
    [HttpGet("subOrder/{subOrderId}")]
    [Authorize(Policy = GatewayPolicies.Customer)]
    public Task<IResult> GetSubOrder(string subOrderId)
    {
        if (string.IsNullOrWhiteSpace(subOrderId))
            throw new GatewayInputException("Sub-order ID is required.", "SubOrderId");

        return ForwardAsync(
            Service,
            $"/api/v1/subOrder/{subOrderId}",
            HttpMethod.Get,
            mockSeed: "sub-order-get"
        );
    }

    /// <summary>Người mua huỷ đơn khi chưa thanh toán (ORD-06)</summary>
    [HttpPost("orders/{orderId}/cancel")]
    [Authorize(Policy = GatewayPolicies.Customer)]
    public Task<IResult> CancelOrder(string orderId, [FromBody] CancelOrderRequest body)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            throw new GatewayInputException("Order ID is required.", "OrderId");

        return ForwardAsync(
            Service,
            $"/api/v1/orders/{orderId}/cancel",
            HttpMethod.Post,
            body: body,
            mockSeed: "order-cancel",
            successMessage: "Order has been successfully cancelled."
        );
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

        return ForwardAsync(
            Service,
            $"/api/v1/orders/{orderId}/accept",
            HttpMethod.Post,
            body: body,
            mockSeed: "sub-order-accept"
        );
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

        return ForwardAsync(
            Service,
            $"/api/v1/orders/{orderId}/reject",
            HttpMethod.Post,
            body: body,
            mockSeed: "sub-order-reject"
        );
    }
}
