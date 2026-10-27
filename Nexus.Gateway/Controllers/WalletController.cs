namespace Nexus.Gateway.Controllers;

using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.Gateway.Common;
using Nexus.Gateway.Dispatching;
using Nexus.Gateway.Dtos.Wallet;
using Nexus.Gateway.Security;

/// <summary>
/// F5 wallet surface (WAL-02, WAL-03, WAL-04, WAL-05B, WAL-07, WAL-08, WAL-09, WAL-10).
/// Routes follow the F5 contract sheet: <c>/api/v1/wallets/*</c> and <c>/api/v1/admin/wallets/*</c>.
/// </summary>
[ApiController]
[Route("api/v1")]
[BackendTag("wallet")]
[EnableRateLimiting("api")]
public sealed class WalletController : GatewayBaseController
{
    private const string Service = "wallet";

    public WalletController(IGatewayDispatcher dispatcher) : base(dispatcher)
    {
    }

    /// <summary>Lấy thông tin ví và số dư khả dụng/bị khóa của cá nhân (WAL-02)</summary>
    [HttpGet("wallets/me")]
    [Authorize(Policy = GatewayPolicies.Authenticated)]
    public Task<IResult> GetMyWallet()
    {
        return ForwardAsync(
            Service,
            "/api/v1/wallets/me",
            HttpMethod.Get,
            mockSeed: "wallet-me",
            successMessage: "Wallet retrieved successfully."
        );
    }

    /// <summary>Tạo yêu cầu nạp tiền qua VNPay và lấy URL thanh toán (WAL-03)</summary>
    [HttpPost("wallets/topups/vnpay")]
    [Authorize(Policy = GatewayPolicies.Authenticated)]
    public Task<IResult> InitiateVnPayTopup([FromBody] TopupVnPayRequest body)
    {
        if (body.Amount < 10000m)
            throw new GatewayInputException("Deposit amount must be at least 10,000 VND.", "Amount", "VAL_INVALID_AMOUNT");

        return ForwardAsync(
            Service,
            "/api/v1/wallets/topups/vnpay",
            HttpMethod.Post,
            body: body,
            mockSeed: "wallet-topup",
            successMessage: "Top-up request created successfully.",
            successStatusCode: StatusCodes.Status201Created
        );
    }

    /// <summary>Webhook nhận kết quả IPN từ VNPay (nạp ví hoặc thanh toán đơn trực tiếp) (WAL-04)</summary>
    /// <remarks>Public callback, không cần đăng nhập; trả nguyên định dạng VNPay, không bọc ApiResponse.</remarks>
    [HttpPost("wallets/vnpay/ipn")]
    [AllowAnonymous]
    public IResult ProcessVnPayIpn()
    {
        return Results.Ok(new VnPayIpnResponse { RspCode = "00", Message = "Confirm success." });
    }

    /// <summary>Tạo phiên thanh toán trực tiếp qua cổng VNPay cho đơn hàng (WAL-05B)</summary>
    [HttpPost("wallets/checkout/direct")]
    [Authorize(Policy = GatewayPolicies.Authenticated)]
    public Task<IResult> InitiateDirectPayment([FromBody] DirectCheckoutRequest body)
    {
        if (string.IsNullOrWhiteSpace(body.OrderId))
            throw new GatewayInputException("Order ID is required.", "OrderId");
        if (body.Amount <= 0)
            throw new GatewayInputException("Payment amount must be greater than zero.", "Amount", "VAL_INVALID_AMOUNT");

        return ForwardAsync(
            Service,
            "/api/v1/wallets/checkout/direct",
            HttpMethod.Post,
            body: body,
            mockSeed: "wallet-checkout-direct",
            successMessage: "Direct payment session created successfully."
        );
    }

    /// <summary>Seller tạo yêu cầu rút tiền từ ví về tài khoản ngân hàng (WAL-07)</summary>
    [HttpPost("wallets/withdrawals")]
    [Authorize(Policy = GatewayPolicies.Seller)]
    public Task<IResult> RequestWithdrawal([FromBody] WithdrawalRequest body)
    {
        if (body.Amount < 50000m)
            throw new GatewayInputException("Minimum withdrawal amount is 50,000 VND.", "Amount", "MIN_WITHDRAWAL_LIMIT");
        if (string.IsNullOrWhiteSpace(body.BankCode))
            throw new GatewayInputException("Bank code is required.", "BankCode");
        if (string.IsNullOrWhiteSpace(body.AccountNumber))
            throw new GatewayInputException("Account number is required.", "AccountNumber");
        if (string.IsNullOrWhiteSpace(body.AccountHolderName))
            throw new GatewayInputException("Account holder name is required.", "AccountHolderName");

        return ForwardAsync(
            Service,
            "/api/v1/wallets/withdrawals",
            HttpMethod.Post,
            body: body,
            mockSeed: "wallet-withdrawal",
            successMessage: "Withdrawal request created successfully.",
            successStatusCode: StatusCodes.Status201Created
        );
    }

    /// <summary>Admin duyệt hoặc từ chối yêu cầu rút tiền của Seller (WAL-08)</summary>
    [HttpPatch("admin/wallets/withdrawals/{withdrawalId}")]
    [Authorize(Policy = GatewayPolicies.Admin)]
    public Task<IResult> ProcessWithdrawal(string withdrawalId, [FromBody] ProcessWithdrawalRequest body)
    {
        if (string.IsNullOrWhiteSpace(withdrawalId))
            throw new GatewayInputException("Withdrawal ID is required.", "WithdrawalId");
        if (body.Action != "APPROVE" && body.Action != "REJECT")
            throw new GatewayInputException("Action must be APPROVE or REJECT.", "Action");

        return ForwardAsync(
            Service,
            $"/api/v1/admin/wallets/withdrawals/{withdrawalId}",
            HttpMethod.Patch,
            body: body,
            mockSeed: "wallet-withdrawal-process",
            successMessage: "Withdrawal request processed successfully."
        );
    }

    /// <summary>Tra cứu lịch sử biến động sổ cái (wallet_transactions) cá nhân (WAL-09)</summary>
    [HttpGet("wallets/transactions")]
    [Authorize(Policy = GatewayPolicies.Authenticated)]
    public Task<IResult> GetTransactions(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] string? type = null,
        [FromQuery] string? fromDate = null,
        [FromQuery] string? toDate = null)
    {
        if (page < 1 || limit < 1 || limit > 100)
            throw new GatewayInputException("Page or limit is invalid.", "Pagination");

        var query = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(type)) query["type"] = type;
        if (!string.IsNullOrWhiteSpace(fromDate)) query["fromDate"] = fromDate;
        if (!string.IsNullOrWhiteSpace(toDate)) query["toDate"] = toDate;

        return ForwardPagedAsync(
            Service,
            "/api/v1/wallets/transactions",
            page,
            limit,
            HttpMethod.Get,
            mockSeed: "wallet-transactions",
            queryParams: query
        );
    }

    /// <summary>Admin điều chỉnh số dư ví thủ công kèm kiểm toán (WAL-10)</summary>
    [HttpPost("admin/wallets/{walletId}/adjustments")]
    [Authorize(Policy = GatewayPolicies.Admin)]
    public Task<IResult> AdjustBalance(string walletId, [FromBody] AdjustBalanceRequest body)
    {
        if (string.IsNullOrWhiteSpace(walletId))
            throw new GatewayInputException("Wallet ID is required.", "WalletId");
        if (body.Amount <= 0)
            throw new GatewayInputException("Adjustment amount must be greater than zero.", "Amount", "VAL_INVALID_AMOUNT");
        if (string.IsNullOrWhiteSpace(body.Reason))
            throw new GatewayInputException("Adjustment reason is required.", "Reason");

        return ForwardAsync(
            Service,
            $"/api/v1/admin/wallets/{walletId}/adjustments",
            HttpMethod.Post,
            body: body,
            mockSeed: "wallet-adjustment",
            successMessage: "Wallet balance adjusted successfully."
        );
    }
}
