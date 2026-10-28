namespace Nexus.Gateway.Dtos.Commerce;

// ============================================================
// COMMERCE REQUEST DTOs — aligned with flow_mock_excel.xlsx F1
// ============================================================

// ---------- CART REQUESTS ----------

/// <summary>CART-02: POST /api/v1/cart/items — thêm sản phẩm vào giỏ</summary>
public sealed class AddCartItemRequest
{
    public string? ProductId { get; set; }
    public string? SkuId { get; set; }
    public int Quantity { get; set; }
}

/// <summary>CART-03: PATCH /api/v1/cart/items/{id} — đổi số lượng</summary>
public sealed class UpdateCartItemRequest
{
    public int Quantity { get; set; }
}

/// <summary>CART-04: DELETE /api/v1/cart/items — xóa 1 hoặc nhiều item</summary>
public sealed class RemoveCartItemsRequest
{
    public List<string>? CartItemIds { get; set; }
    public bool All { get; set; }
}

/// <summary>CART-05: DELETE /api/v1/cart/items/all — xóa toàn bộ</summary>
public sealed class ClearCartRequest
{
    public bool All { get; set; }
}

// ---------- CHECKOUT REQUESTS ----------

/// <summary>CHK-01: POST /api/v1/checkout/quote</summary>
public sealed class CheckoutQuoteRequest
{
    public List<string>? CartItemIds { get; set; }
    public ShippingAddressDto? ShippingAddress { get; set; }
    public string? ContactPhone { get; set; }
    public string? PaymentMethod { get; set; } // WALLET | VNPAY
    public string? CarrierCode { get; set; }
    public VouchersDto? Vouchers { get; set; }
}

public sealed class ShippingAddressDto
{
    public string? ReceiverName { get; set; }
    public string? Street { get; set; }
    public string? Ward { get; set; }
    public string? District { get; set; }
    public string? Province { get; set; }
    public int? GhnDistrictId { get; set; }
    public string? GhnWardCode { get; set; }
}

public sealed class VouchersDto
{
    public string? System { get; set; }
    public List<ShopVoucherDto>? Shops { get; set; }
    public List<ProductVoucherDto>? Products { get; set; }
}

public sealed class ShopVoucherDto
{
    public string? SellerId { get; set; }
    public string? Code { get; set; }
}

public sealed class ProductVoucherDto
{
    public string? SkuId { get; set; }
    public string? Code { get; set; }
}

/// <summary>ORD-01: POST /api/v1/orders — tạo order</summary>
public sealed class CreateOrderRequest
{
    public string? QuoteId { get; set; }
    public string? PaymentMethod { get; set; } // WALLET | VNPAY | COD
    public string? ShippingAddress { get; set; }
    public string? ContactPhone { get; set; }
    public string? Note { get; set; }
}

/// <summary>ORD-04: POST /api/v1/orders/{orderId}/cancel — hủy đơn</summary>
public sealed class CancelOrderRequest
{
    public string? ReasonCode { get; set; } // BUYER_CHANGED_MIND | FOUND_BETTER_PRICE | OTHER
    public string? ReasonText { get; set; }
}

/// <summary>Seller accept/reject sub-order</summary>
public sealed class SubOrderDecisionRequest
{
    public string? SubOrderId { get; set; }
    public string? Note { get; set; }
    public string? ReasonCode { get; set; }
    public string? ReasonText { get; set; }
}

/// <summary>ORD-11: POST /api/v1/subOrder/{subOrderId}/refund — hoàn tiền sub-order</summary>
public sealed class RefundSubOrderRequest
{
    public string? Reason { get; set; } // DEFECTIVE_PRODUCT | MISSING_ITEMS | OTHER
    public string? Note { get; set; }
}

// ---------- REVIEW / FEEDBACK REQUESTS ----------

/// <summary>ORD-07: POST /api/v1/order/{orderId}/products/{productId}/reviews</summary>
public sealed class CreateProductReviewRequest
{
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public List<string>? Images { get; set; }
}

/// <summary>ORD-08: POST /api/v1/order/{orderId}/user/{userId} — đánh giá người dùng</summary>
public sealed class CreateUserFeedbackRequest
{
    public int Rating { get; set; }
    public string? Feedback { get; set; }
}

/// <summary>PATCH /api/v1/order/{orderId}/products/{productId}/reviews</summary>
public sealed class UpdateProductReviewRequest
{
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public List<string>? Images { get; set; }
}

/// <summary>PATCH /api/v1/order/{orderId}/user/{userId}</summary>
public sealed class UpdateUserFeedbackRequest
{
    public int Rating { get; set; }
    public string? Feedback { get; set; }
}

// ============================================================
// COMMERCE RESPONSE DTOs — aligned with flow_mock_excel.xlsx F1
// ============================================================

// ---------- CART RESPONSES ----------

/// <summary>CART-02: POST /api/v1/cart/items — 200 OK</summary>
public sealed class AddCartItemResponseDto
{
    public string? CartItemId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPriceSnapshot { get; set; }
    public CartSummaryDto? Cart { get; set; }
}

/// <summary>CART-03: PATCH /api/v1/cart/items/{id} — 200 OK</summary>
public sealed class UpdateCartItemResponseDto
{
    public string? CartItemId { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
    public CartSummaryDto? Cart { get; set; }
}

/// <summary>CART-04: DELETE /api/v1/cart/items — 200 OK</summary>
public sealed class RemoveCartItemsResponseDto
{
    public CartSummaryDto? Cart { get; set; }
}

/// <summary>CART-05: DELETE /api/v1/cart/items/all — 200 OK</summary>
public sealed class ClearCartResponseDto
{
    public int RemovedCount { get; set; }
    public CartSummaryDto? Cart { get; set; }
}

public sealed class CartSummaryDto
{
    public int ItemCount { get; set; }
    public decimal SubtotalAmount { get; set; }
}

/// <summary>CART-01: GET /api/v1/cart — 200 OK</summary>
public sealed class CartResponseDto
{
    public string? CartId { get; set; }
    public string? Status { get; set; } // Active | Abandoned | Converted
    public int ItemCount { get; set; }
    public decimal SubtotalAmount { get; set; }
    public string? LastActivityAt { get; set; }
    public List<CartGroupDto>? Groups { get; set; }
    public List<string>? Warnings { get; set; }
}

public sealed class CartGroupDto
{
    public string? SellerId { get; set; }
    public string? SellerName { get; set; }
    public List<CartItemDto>? Items { get; set; }
}

public sealed class CartItemDto
{
    public string? CartItemId { get; set; }
    public string? ProductId { get; set; }
    public string? SkuId { get; set; }
    public string? ProductName { get; set; }
    public object? SkuAttributes { get; set; }
    public string? ImageUrl { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPriceSnapshot { get; set; }
    public decimal CurrentUnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public bool PriceChanged { get; set; }
    public int StockAvailable { get; set; }
    public string? ItemStatus { get; set; } // OK | OUT_OF_STOCK | INACTIVE | PRICE_CHANGED
}

/// <summary>CART-06: GET /api/v1/cart/items/select — 200 OK (item)</summary>
public sealed class CartItemValidationDto
{
    public string? CartItemId { get; set; }
    public List<string>? Issues { get; set; }
    public decimal CurrentUnitPrice { get; set; }
    public int AvailableQty { get; set; }
}

// ---------- CHECKOUT / ORDER RESPONSES ----------

/// <summary>CHK-01: POST /api/v1/checkout/quote — 200 OK</summary>
public sealed class CheckoutQuoteResponseDto
{
    public List<OrderQuoteDto>? Orders { get; set; }
    public decimal GrandTotal { get; set; }
    public object[]? AppliedVouchers { get; set; }
    public List<InvalidVoucherDto>? InvalidVouchers { get; set; }
    public decimal WalletBalance { get; set; }
    public List<string>? Warnings { get; set; }
}

public sealed class OrderQuoteDto
{
    public string? SellerId { get; set; }
    public object[]? Items { get; set; }
    public decimal ItemsAmount { get; set; }
    public decimal ProductDiscountAmount { get; set; }
    public decimal ShopDiscountAmount { get; set; }
    public decimal PlatformMarkupAmount { get; set; }
    public decimal SystemDiscountAmount { get; set; }
    public decimal ShippingFeeAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public List<ShippingOptionDto>? ShippingOptions { get; set; }
    public string? QuoteId { get; set; }
}

public sealed class ShippingOptionDto
{
    public decimal Fee { get; set; }
    public string? ServiceType { get; set; } // Standard | Express | SameDay
    public string? EstimatedDeliveryAt { get; set; }
}

public sealed class InvalidVoucherDto
{
    public string? Code { get; set; }
    public string? Reason { get; set; }
}

/// <summary>ORD-01: POST /api/v1/orders — 201 Created</summary>
public sealed class CreateOrderResponseDto
{
    public string? OrderId { get; set; }
    public string? OrderNo { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Status { get; set; }
    public string? PaymentDeadlineAt { get; set; }
    public string? PaymentMethod { get; set; }
    public string? PaymentUrl { get; set; }
    public List<string>? SubOrderIds { get; set; }
    public List<SubOrderSummaryDto>? SubOrders { get; set; }
    public string? CreatedAt { get; set; }
}

public sealed class SubOrderSummaryDto
{
    public string? SubOrderId { get; set; }
    public string? SellerId { get; set; }
    public string? SellerName { get; set; }
    public decimal Amount { get; set; }
    public int ItemCount { get; set; }
}

/// <summary>ORD-02: GET /api/v1/orders/{orderId}</summary>
public sealed class OrderDetailResponseDto
{
    public string? OrderId { get; set; }
    public string? OrderNo { get; set; }
    public string? BuyerId { get; set; }
    public string? Status { get; set; }
    public decimal TotalAmount { get; set; }
    public string? PaymentMethod { get; set; }
    public string? ShippingAddress { get; set; }
    public string? ContactPhone { get; set; }
    public string? CreatedAt { get; set; }
    public List<SubOrderDetailDto>? SubOrders { get; set; }
}

public sealed class SubOrderDetailDto
{
    public string? SubOrderId { get; set; }
    public string? SubOrderNo { get; set; }
    public string? SellerId { get; set; }
    public string? SellerName { get; set; }
    public string? ShopName { get; set; }
    public string? Status { get; set; }
    public decimal ItemsAmount { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal Amount { get; set; }
    public List<OrderItemDto>? Items { get; set; }
}

public sealed class OrderItemDto
{
    public string? OrderItemId { get; set; }
    public string? ProductId { get; set; }
    public string? SkuId { get; set; }
    public string? ProductName { get; set; }
    public string? SkuCode { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

/// <summary>ORD-03: GET /api/v1/subOrder/{subOrderId}</summary>
public sealed class SubOrderItemsResponseDto
{
    public List<OrderItemDto>? Items { get; set; }
    public PaginationDto? Pagination { get; set; }
}

/// <summary>ORD-04: POST /api/v1/orders/{orderId}/cancel — 200 OK</summary>
public sealed class CancelOrderResponseDto
{
    public string? OrderId { get; set; }
    public string? Status { get; set; }
    public string? CancelledAt { get; set; }
}

/// <summary>ORD-11: POST /api/v1/subOrder/{subOrderId}/refund — 200 OK</summary>
public sealed class RefundSubOrderResponseDto
{
    public string? SubOrderId { get; set; }
    public decimal RefundAmount { get; set; }
    public string? Status { get; set; }
    public string? RefundedAt { get; set; }
}

// ---------- PRODUCT REVIEW RESPONSES ----------

/// <summary>ORD-07: POST /api/v1/order/{orderId}/products/{productId}/reviews — 201 Created</summary>
public sealed class CreateProductReviewResponseDto
{
    public string? ReviewId { get; set; }
    public string? OrderId { get; set; }
    public string? ProductId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string? CreatedAt { get; set; }
}

/// <summary>PATCH /api/v1/order/{orderId}/products/{productId}/reviews — 200 OK</summary>
public sealed class UpdateProductReviewResponseDto
{
    public string? ReviewId { get; set; }
    public string? OrderId { get; set; }
    public string? ProductId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string? UpdatedAt { get; set; }
}

/// <summary>ORD-08: POST /api/v1/order/{orderId}/user/{userId} — 201 Created</summary>
public sealed class CreateUserFeedbackResponseDto
{
    public string? FeedbackId { get; set; }
    public string? OrderId { get; set; }
    public string? TargetUserId { get; set; }
    public int Rating { get; set; }
    public string? CreatedAt { get; set; }
}

/// <summary>PATCH /api/v1/order/{orderId}/user/{userId} — 200 OK</summary>
public sealed class UpdateUserFeedbackResponseDto
{
    public string? FeedbackId { get; set; }
    public string? OrderId { get; set; }
    public string? TargetUserId { get; set; }
    public int Rating { get; set; }
    public string? UpdatedAt { get; set; }
}

// ---------- SHARED ----------

public sealed class PaginationDto
{
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasPreviousPage { get; set; }
    public bool HasNextPage { get; set; }
}

/// <summary>POST /api/v1/webhooks/payment — payment gateway IPN/webhook</summary>
public sealed class PaymentWebhookRequest
{
    public string? Provider { get; set; }
    public string? TransactionId { get; set; }
    public string? OrderCode { get; set; }
    public string? Status { get; set; }
    public decimal Amount { get; set; }
    public string? SecureHash { get; set; }
    public object? RawPayload { get; set; }
}
