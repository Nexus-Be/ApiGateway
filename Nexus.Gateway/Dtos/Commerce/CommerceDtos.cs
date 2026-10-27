namespace Nexus.Gateway.Dtos.Commerce;

// Request bodies for the F1 commerce surface (cart, checkout, orders).
// The response contracts live in Seeds/commerce/*.json because the gateway serves
// this feature in Mock mode; typed gRPC contracts are Track C work.

public sealed class AddCartItemRequest
{
    public string? ProductId { get; set; }
    public string? SkuId { get; set; }
    public int Quantity { get; set; }
}

public sealed class UpdateCartItemRequest
{
    public int Quantity { get; set; }
}

public sealed class RemoveCartItemsRequest
{
    public List<string>? CartItemIds { get; set; }
    public bool All { get; set; }
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

public sealed class VouchersDto
{
    public string? System { get; set; }
    public List<ShopVoucherDto>? Shops { get; set; }
    public List<ProductVoucherDto>? Products { get; set; }
}

public sealed class CheckoutQuoteRequest
{
    public List<string>? CartItemIds { get; set; }
    public ShippingAddressDto? ShippingAddress { get; set; }
    public string? ContactPhone { get; set; }
    public string? PaymentMethod { get; set; }
    public string? CarrierCode { get; set; }
    public VouchersDto? Vouchers { get; set; }
}

public sealed class CreateOrderRequest
{
    public string? QuoteId { get; set; }
    public string? PaymentMethod { get; set; }
    public string? ShippingAddress { get; set; }
    public string? ContactPhone { get; set; }
    public string? Note { get; set; }
}

public sealed class CancelOrderRequest
{
    public string? ReasonCode { get; set; }
    public string? ReasonText { get; set; }
}

public sealed class SubOrderDecisionRequest
{
    public string? SubOrderId { get; set; }
    public string? Note { get; set; }
    public string? ReasonCode { get; set; }
    public string? ReasonText { get; set; }
}

public sealed class PaymentWebhookRequest
{
    public string? OrderId { get; set; }
    public string? TransactionId { get; set; }
    public decimal? Amount { get; set; }
    public string? Status { get; set; }
    public string? PaymentMethod { get; set; }
    public DateTime? PaidAt { get; set; }
}

public sealed class CreateProductReviewRequest
{
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public List<string>? Images { get; set; }
}

public sealed class CreateUserFeedbackRequest
{
    public int Rating { get; set; }
    public string? Feedback { get; set; }
}

public sealed class UpdateProductReviewRequest
{
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public List<string>? Images { get; set; }
}

public sealed class UpdateUserFeedbackRequest
{
    public int Rating { get; set; }
    public string? Feedback { get; set; }
}

public sealed class RefundSubOrderRequest
{
    public string? Reason { get; set; }
    public string? Note { get; set; }
}
