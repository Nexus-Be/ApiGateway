namespace Nexus.Gateway.Dtos.Wallet;

using System.Text.Json.Serialization;

// Request bodies for the F5 wallet surface (WAL-02 .. WAL-10). Response contracts live in
// Seeds/wallet/*.json because the gateway serves this feature in Mock mode; typed gRPC contracts
// for Wallet are Track C work.

/// <summary>WAL-03 — tạo yêu cầu nạp tiền qua VNPay.</summary>
public sealed class TopupVnPayRequest
{
    public decimal Amount { get; set; }
    public string? BankCode { get; set; }
    public string? ReturnUrl { get; set; }
}

/// <summary>WAL-05B — thanh toán trực tiếp đơn hàng qua cổng giao dịch.</summary>
public sealed class DirectCheckoutRequest
{
    public string? OrderId { get; set; }
    public decimal Amount { get; set; }
    public string? Gateway { get; set; }
    public string? BankCode { get; set; }
    public string? ReturnUrl { get; set; }
}

/// <summary>WAL-07 — Seller tạo yêu cầu rút tiền về tài khoản ngân hàng.</summary>
public sealed class WithdrawalRequest
{
    public decimal Amount { get; set; }
    public string? BankCode { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountHolderName { get; set; }
}

/// <summary>WAL-08 — Admin duyệt hoặc từ chối yêu cầu rút tiền.</summary>
public sealed class ProcessWithdrawalRequest
{
    public string? Action { get; set; }
    public string? AdminNote { get; set; }
}

/// <summary>WAL-10 — Admin điều chỉnh số dư ví thủ công kèm kiểm toán.</summary>
public sealed class AdjustBalanceRequest
{
    public decimal Amount { get; set; }
    public string? Reason { get; set; }
    public string? AdjustmentType { get; set; }
}

/// <summary>
/// WAL-04 — phản hồi IPN chuẩn VNPay. Đây là callback gateway công khai, trả nguyên định dạng
/// <c>{ "RspCode", "Message" }</c> của VNPay (không bọc <c>ApiResponse&lt;T&gt;</c>).
/// </summary>
public sealed class VnPayIpnResponse
{
    [JsonPropertyName("RspCode")]
    public string? RspCode { get; set; }

    [JsonPropertyName("Message")]
    public string? Message { get; set; }
}
