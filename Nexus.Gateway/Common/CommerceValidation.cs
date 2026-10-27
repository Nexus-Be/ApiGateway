namespace Nexus.Gateway.Common;

using System;
using System.Collections.Generic;

/// <summary>
/// Gateway-owned validation for the F1 commerce surface (orders, checkout, cart).
/// Matches real Commerce.Domain.Enums contracts.
/// </summary>
public static class CommerceValidation
{
    public const string ErrorCode = "VAL_INVALID_INPUT";

    public static readonly string[] OrderStatuses =
        ["PendingPayment", "PaymentProcessing", "Paid", "Cancelled", "Expired"];

    public static readonly string[] SubOrderStatuses =
        ["PendingPayment", "Paid", "Confirmed", "SellerAccepted", "Shipping", "Shipped", "Delivered", "Completed", "Cancelled", "Refunded"];

    public static readonly string[] PaymentMethods =
        ["Wallet", "VnPay", "Cod"];

    public static readonly string[] CancellationReasonCodes =
        ["BuyerChangedMind", "OutOfStock", "PaymentTimeout", "SellerRejected", "Other"];

    public static readonly string[] CartStatuses =
        ["Active", "CheckedOut", "Abandoned"];

    public static void EnsureOptionalOrderStatus(string? status, string field = "status")
    {
        if (string.IsNullOrWhiteSpace(status)) return;
        EnumValidationHelper.EnsureOneOf(status, field, OrderStatuses);
    }

    public static void EnsureOptionalPaymentMethod(string? paymentMethod, string field = "paymentMethod")
    {
        if (string.IsNullOrWhiteSpace(paymentMethod)) return;
        EnumValidationHelper.EnsureOneOf(paymentMethod, field, PaymentMethods);
    }

    public static void EnsureOptionalCancellationReason(string? reasonCode, string field = "reasonCode")
    {
        if (string.IsNullOrWhiteSpace(reasonCode)) return;
        EnumValidationHelper.EnsureOneOf(reasonCode, field, CancellationReasonCodes);
    }
}
