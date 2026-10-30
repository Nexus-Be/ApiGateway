namespace Nexus.Gateway.Dtos.Wallet;

using System.Text.Json;
using System.Text.Json.Serialization;

// ============================================================
// WALLET REQUEST DTOs — aligned with flow_mock_excel.xlsx F5
// ============================================================

/// <summary>WAL-03: POST /api/v1/wallets/topups/vnpay - initiate VNPay top-up</summary>
public sealed class CreateVnpayTopupRequest
{
    public decimal Amount { get; set; }
    public string? BankCode { get; set; }
    public string? ReturnUrl { get; set; }
}

/// <summary>WAL-04: POST /api/v1/wallets/vnpay/ipn - VNPay IPN webhook</summary>
public sealed class VnpayIpnRequest
{
    public string? VnpTmnCode { get; set; }
    public string? VnpAmount { get; set; }
    public string? VnpBankCode { get; set; }
    public string? VnpBankTranNo { get; set; }
    public string? VnpCardType { get; set; }
    public string? VnpOrderInfo { get; set; }
    public string? VnpPayDate { get; set; }
    public string? VnpResponseCode { get; set; }
    public string? VnpTransactionNo { get; set; }
    public string? VnpTransactionStatus { get; set; }
    public string? VnpTxnRef { get; set; }
    public string? VnpSecureHash { get; set; }
}

/// <summary>WAL-05B: POST /api/v1/wallets/checkout/direct - direct payment via VNPay</summary>
public sealed class DirectCheckoutRequest
{
    public string? OrderId { get; set; }
    public decimal Amount { get; set; }
    public string? Gateway { get; set; }  // VNPAY
    public string? BankCode { get; set; }
    public string? ReturnUrl { get; set; }
}

/// <summary>WAL-07: POST /api/v1/wallets/withdrawals - seller withdrawal request</summary>
public sealed class CreateWithdrawalRequest
{
    public decimal Amount { get; set; }
    public string? BankCode { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountHolderName { get; set; }
}

/// <summary>WAL-08: PATCH /api/v1/admin/wallets/withdrawals/{id} - approve or reject withdrawal</summary>
public sealed class ProcessWithdrawalRequest
{
    public string? Action { get; set; }    // APPROVE | REJECT
    public string? AdminNote { get; set; }
}

/// <summary>WAL-10: POST /api/v1/admin/wallets/{walletId}/adjustments - admin balance adjustment</summary>
public sealed class WalletAdjustmentRequest
{
    public decimal Amount { get; set; }
    public string? Reason { get; set; }
    public string? AdjustmentType { get; set; }  // CREDIT | DEBIT
    public string? Direction { get => AdjustmentType; set => AdjustmentType = value; }
}

// ============================================================
// WALLET RESPONSE DTOs — aligned with flow_mock_excel.xlsx F5
// ============================================================

/// <summary>WAL-02: GET /api/v1/wallets/me — 200 OK</summary>
public sealed class WalletInfoResponseDto
{
    public string? WalletId { get; set; }
    public string? UserId { get; set; }
    public decimal Balance { get; set; }
    public decimal LockedBalance { get; set; }
    public decimal AvailableBalance { get; set; }
    public string? Currency { get; set; }
    public string? Status { get; set; }   // ACTIVE | LOCKED | FROZEN
    public string? UpdatedAt { get; set; }
}

/// <summary>WAL-03: POST /api/v1/wallets/topups/vnpay — 201 Created</summary>
public sealed class VnpayTopupCreatedResponseDto
{
    public string? TopupId { get; set; }
    public string? PaymentUrl { get; set; }
    public string? TxnRef { get; set; }
    public int ExpiresIn { get; set; }    // seconds
}

/// <summary>WAL-04: POST /api/v1/wallets/vnpay/ipn — 200 OK (VNPay IPN format)</summary>
public sealed class VnpayIpnResponseDto
{
    public string? RspCode { get; set; }
    public string? Message { get; set; }
}

/// <summary>WAL-05B: POST /api/v1/wallets/checkout/direct — 200 OK</summary>
public sealed class DirectCheckoutResponseDto
{
    public string? TransactionId { get; set; }
    public string? OrderId { get; set; }
    public decimal Amount { get; set; }
    public string? PaymentUrl { get; set; }
    public string? Status { get; set; }    // PENDING
    public string? CreatedAt { get; set; }
}

/// <summary>WAL-07: POST /api/v1/wallets/withdrawals — 201 Created</summary>
public sealed class WithdrawalCreatedResponseDto
{
    public string? WithdrawalId { get; set; }
    public decimal Amount { get; set; }
    public decimal Fee { get; set; }
    public string? Status { get; set; }    // PENDING
    public string? RequestedAt { get; set; }
}

/// <summary>WAL-08: PATCH /api/v1/admin/wallets/withdrawals/{id} — 200 OK</summary>
public sealed class WithdrawalProcessedResponseDto
{
    public string? WithdrawalId { get; set; }
    public string? Status { get; set; }    // APPROVED | REJECTED
    public string? ProcessedAt { get; set; }
    public string? TransactionId { get; set; }
}

/// <summary>WAL-09: GET /api/v1/wallets/transactions — list item</summary>
public sealed class WalletTransactionItemDto
{
    public string? Id { get; set; }
    public string? Type { get; set; }      // DEPOSIT | WITHDRAWAL | PAYMENT | REFUND
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? Status { get; set; }    // SUCCESS | PENDING | FAILED
    public string? Note { get; set; }
    public string? CreatedAt { get; set; }
}

/// <summary>WAL-10: POST /api/v1/admin/wallets/{walletId}/adjustments — 200 OK</summary>
public sealed class WalletAdjustmentResponseDto
{
    public string? AdjustmentId { get; set; }
    public string? WalletId { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? CreatedAt { get; set; }
}

// ---------- SHARED PAGINATION ----------

public sealed class PaginationDto
{
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasPreviousPage { get; set; }
    public bool HasNextPage { get; set; }
}

// ---------- CONTROLLER-FACING ALIASES (short names) ----------

/// <summary>POST /api/v1/admin/wallets/{id}/adjust — alias for WalletAdjustmentRequest</summary>
public sealed class AdjustBalanceRequest
{
    public decimal Amount { get; set; }
    public string? Reason { get; set; }
    public string? AdjustmentType { get; set; } // "CREDIT" | "DEBIT"
    public string? Direction { get => AdjustmentType; set => AdjustmentType = value; }
}

/// <summary>POST /api/v1/wallet/topup/vnpay — alias for CreateVnpayTopupRequest</summary>
public sealed class TopupVnPayRequest
{
    public decimal Amount { get; set; }
    public string? BankCode { get; set; }
    public string? ReturnUrl { get; set; }
    public string? IpAddress { get; set; }
}

/// <summary>POST /api/v1/wallet/withdraw — alias for CreateWithdrawalRequest</summary>
public sealed class WithdrawalRequest
{
    public decimal Amount { get; set; }
    public string? BankCode { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountName { get; set; }
    public string? AccountHolderName { get; set; }
}

/// <summary>POST /api/v1/webhooks/vnpay — IPN response, alias for VnpayIpnRequest</summary>
public sealed class VnPayIpnResponse
{
    public string? VnpTmnCode { get; set; }
    public string? VnpTxnRef { get; set; }
    public string? VnpAmount { get; set; }
    public string? VnpResponseCode { get; set; }
    public string? VnpTransactionStatus { get; set; }
    public string? VnpSecureHash { get; set; }
    public string? VnpOrderInfo { get; set; }
    public string? VnpPayDate { get; set; }

    [JsonPropertyName("RspCode")]
    public string? RspCode { get; set; }

    [JsonPropertyName("Message")]
    public string? Message { get; set; }
}
