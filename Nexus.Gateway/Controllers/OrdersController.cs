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
using Nexus.Gateway.Responses;
using Nexus.Gateway.Security;

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

    /// <summary>Lists orders for the authenticated customer.</summary>
    [HttpGet("orders")]
    [Authorize(GatewayPolicies.Customer)]
    [ProducesResponseType(typeof(ApiResponse<PagedData<OrderListItemDto>>), StatusCodes.Status200OK)]
    public Task<IResult> ListOrders(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] string? status = null,
        [FromQuery] string? fromDate = null,
        [FromQuery] string? toDate = null)
    {
        pagination.EnsureValid();
        CommerceValidation.EnsureOptionalOrderStatus(status);

        return ForwardPagedAsync(
            Service,
            "/api/v1/orders",
            pagination.Page,
            pagination.PageSize,
            HttpMethod.Get,
            mockSeed: "order-list",
            queryParams: new Dictionary<string, string?>
            {
                ["page"] = pagination.Page.ToString(),
                ["pageSize"] = pagination.PageSize.ToString(),
                ["status"] = status,
                ["fromDate"] = fromDate,
                ["toDate"] = toDate
            }
        );
    }

    /// <summary>Calculates checkout quote including discounts and shipping.</summary>
    [HttpPost("checkout/quote")]
    [Authorize(GatewayPolicies.Customer)]
    [ProducesResponseType(typeof(ApiResponse<CheckoutQuoteResponseDto>), StatusCodes.Status200OK)]
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
        CommerceValidation.EnsureOptionalPaymentMethod(body.PaymentMethod);

        return ForwardAsync(
            Service,
            "/api/v1/checkout/quote",
            HttpMethod.Post,
            body: body,
            mockSeed: "checkout-quote"
        );
    }

    /// <summary>Creates an order from a calculated checkout quote.</summary>
    [HttpPost("orders")]
    [Authorize(GatewayPolicies.Customer)]
    [ProducesResponseType(typeof(ApiResponse<CreateOrderResponseDto>), StatusCodes.Status201Created)]
    public Task<IResult> CreateOrder([FromBody] CreateOrderRequest body)
    {
        if (string.IsNullOrWhiteSpace(body.QuoteId))
            throw new GatewayInputException("Checkout quote ID is required.", "QuoteId");
        if (string.IsNullOrWhiteSpace(body.ShippingAddress) || string.IsNullOrWhiteSpace(body.ContactPhone))
            throw new GatewayInputException("Shipping address and phone number are required.", "ShippingAddress");
        CommerceValidation.EnsureOptionalPaymentMethod(body.PaymentMethod);

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

    /// <summary>Gets order details by ID.</summary>
    [HttpGet("order/{orderId}")]
    [Authorize(GatewayPolicies.Customer)]
    [ProducesResponseType(typeof(ApiResponse<OrderDetailResponseDto>), StatusCodes.Status200OK)]
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

    /// <summary>Gets sub-order details by ID.</summary>
    [HttpGet("subOrder/{subOrderId}")]
    [Authorize(GatewayPolicies.Customer)]
    [ProducesResponseType(typeof(ApiResponse<SubOrderDetailDto>), StatusCodes.Status200OK)]
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

    /// <summary>Cancels an unpaid order.</summary>
    [HttpPost("orders/{orderId}/cancel")]
    [Authorize(GatewayPolicies.Customer)]
    [ProducesResponseType(typeof(ApiResponse<CancelOrderResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> CancelOrder(string orderId, [FromBody] CancelOrderRequest body)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            throw new GatewayInputException("Order ID is required.", "OrderId");
        CommerceValidation.EnsureOptionalCancellationReason(body.ReasonCode);

        return ForwardAsync(
            Service,
            $"/api/v1/orders/{orderId}/cancel",
            HttpMethod.Post,
            body: body,
            mockSeed: "order-cancel",
            successMessage: "Order has been successfully cancelled."
        );
    }

    /// <summary>Accepts a sub-order by seller.</summary>
    [HttpPost("orders/{orderId}/accept")]
    [Authorize(GatewayPolicies.Seller)]
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

    /// <summary>Rejects a sub-order by seller.</summary>
    [HttpPost("orders/{orderId}/reject")]
    [Authorize(GatewayPolicies.Seller)]
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

    /// <summary>Processes payment webhook notification from payment gateway.</summary>
    [HttpPost("payments/webhook")]
    [AllowAnonymous]
    public Task<IResult> PaymentWebhook([FromBody] PaymentWebhookRequest body)
    {
        return ForwardAsync(
            Service,
            "/api/v1/payments/webhook",
            HttpMethod.Post,
            body: body,
            mockSeed: "payment-webhook",
            successStatusCode: StatusCodes.Status200OK,
            successMessage: "Payment webhook processed successfully."
        );
    }

    /// <summary>Submits a product review after order completion.</summary>
    [HttpPost("order/{orderId}/products/{productId}/reviews")]
    [Authorize(GatewayPolicies.Customer)]
    public Task<IResult> CreateProductReview(
        [FromRoute] string orderId,
        [FromRoute] string productId,
        [FromBody] CreateProductReviewRequest body)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            throw new GatewayInputException("Order ID is required.", "OrderId");
        if (string.IsNullOrWhiteSpace(productId))
            throw new GatewayInputException("Product ID is required.", "ProductId");
        if (body.Rating < 1 || body.Rating > 5)
            throw new GatewayInputException("Rating must be between 1 and 5.", "Rating", "VAL_INVALID_RATING");

        return ForwardAsync(
            Service,
            $"/api/v1/order/{orderId}/products/{productId}/reviews",
            HttpMethod.Post,
            body: body,
            mockSeed: "order-product-review",
            successStatusCode: StatusCodes.Status201Created,
            successMessage: "Product review created successfully."
        );
    }

    /// <summary>Submits user feedback for an order.</summary>
    [HttpPost("order/{orderId}/user/{userId}")]
    [Authorize(GatewayPolicies.Customer)]
    public Task<IResult> CreateUserFeedback(
        [FromRoute] string orderId,
        [FromRoute] string userId,
        [FromBody] CreateUserFeedbackRequest body)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            throw new GatewayInputException("Order ID is required.", "OrderId");
        if (string.IsNullOrWhiteSpace(userId))
            throw new GatewayInputException("User ID is required.", "UserId");
        if (body.Rating < 1 || body.Rating > 5)
            throw new GatewayInputException("Rating must be between 1 and 5.", "Rating", "VAL_INVALID_RATING");

        return ForwardAsync(
            Service,
            $"/api/v1/order/{orderId}/user/{userId}",
            HttpMethod.Post,
            body: body,
            mockSeed: "order-user-feedback",
            successStatusCode: StatusCodes.Status201Created,
            successMessage: "User feedback created successfully."
        );
    }

    /// <summary>Updates an existing product review.</summary>
    [HttpPatch("order/{orderId}/products/{productId}/reviews")]
    [Authorize(GatewayPolicies.Customer)]
    public Task<IResult> UpdateProductReview(
        [FromRoute] string orderId,
        [FromRoute] string productId,
        [FromBody] UpdateProductReviewRequest body)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            throw new GatewayInputException("Order ID is required.", "OrderId");
        if (string.IsNullOrWhiteSpace(productId))
            throw new GatewayInputException("Product ID is required.", "ProductId");
        if (body.Rating < 1 || body.Rating > 5)
            throw new GatewayInputException("Rating must be between 1 and 5.", "Rating", "VAL_INVALID_RATING");

        return ForwardAsync(
            Service,
            $"/api/v1/order/{orderId}/products/{productId}/reviews",
            HttpMethod.Patch,
            body: body,
            mockSeed: "order-product-review-update",
            successMessage: "Product review updated successfully."
        );
    }

    /// <summary>Updates existing user feedback.</summary>
    [HttpPatch("order/{orderId}/user/{userId}")]
    [Authorize(GatewayPolicies.Customer)]
    public Task<IResult> UpdateUserFeedback(
        [FromRoute] string orderId,
        [FromRoute] string userId,
        [FromBody] UpdateUserFeedbackRequest body)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            throw new GatewayInputException("Order ID is required.", "OrderId");
        if (string.IsNullOrWhiteSpace(userId))
            throw new GatewayInputException("User ID is required.", "UserId");
        if (body.Rating < 1 || body.Rating > 5)
            throw new GatewayInputException("Rating must be between 1 and 5.", "Rating", "VAL_INVALID_RATING");

        return ForwardAsync(
            Service,
            $"/api/v1/order/{orderId}/user/{userId}",
            HttpMethod.Patch,
            body: body,
            mockSeed: "order-user-feedback-update",
            successMessage: "User feedback updated successfully."
        );
    }

    /// <summary>Requests a refund for a sub-order.</summary>
    [HttpPost("subOrder/{subOrderId}/refund")]
    [Authorize(GatewayPolicies.Customer)]
    public Task<IResult> RefundSubOrder(
        [FromRoute] string subOrderId,
        [FromBody] RefundSubOrderRequest body)
    {
        if (string.IsNullOrWhiteSpace(subOrderId))
            throw new GatewayInputException("Sub-order ID is required.", "SubOrderId");
        if (string.IsNullOrWhiteSpace(body.Reason))
            throw new GatewayInputException("Reason is required.", "Reason");

        return ForwardAsync(
            Service,
            $"/api/v1/subOrder/{subOrderId}/refund",
            HttpMethod.Post,
            body: body,
            mockSeed: "sub-order-refund",
            successStatusCode: StatusCodes.Status200OK,
            successMessage: "Sub-order refund requested successfully."
        );
    }
}
