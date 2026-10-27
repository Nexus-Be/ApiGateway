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

    /// <summary>Lấy danh sách đơn hàng của người mua (Row 78 / ORD-07)</summary>
    [HttpGet("orders")]
    [HttpGet("order")]
    [Authorize(Policy = GatewayPolicies.Customer)]
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
        CommerceValidation.EnsureOptionalPaymentMethod(body.PaymentMethod);

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

    /// <summary>Webhook thông báo kết quả thanh toán từ payment gateway (Row 79 / COM-08)</summary>
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

    /// <summary>Đánh giá sản phẩm sau khi đơn hoàn tất (Row 90 / COM-19)</summary>
    [HttpPost("order/{orderId}/products/{productId}/reviews")]
    [Authorize(Policy = GatewayPolicies.Customer)]
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

    /// <summary>Đánh giá người bán/người mua theo đơn hàng (Row 91 / COM-20)</summary>
    [HttpPost("order/{orderId}/user/{userId}")]
    [Authorize(Policy = GatewayPolicies.Customer)]
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

    /// <summary>Cập nhật đánh giá sản phẩm (Row 92 / COM-21)</summary>
    [HttpPatch("order/{orderId}/products/{productId}/reviews")]
    [Authorize(Policy = GatewayPolicies.Customer)]
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

    /// <summary>Cập nhật đánh giá người dùng (Row 93 / COM-22)</summary>
    [HttpPatch("order/{orderId}/user/{userId}")]
    [Authorize(Policy = GatewayPolicies.Customer)]
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

    /// <summary>Yêu cầu hoàn tiền đơn hàng con (Row 97 / COM-26)</summary>
    [HttpPost("subOrder/{subOrderId}/refund")]
    [Authorize(Policy = GatewayPolicies.Customer)]
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
