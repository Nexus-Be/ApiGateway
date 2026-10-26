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
using Nexus.Gateway.Dtos.Wallet;
using Nexus.Gateway.Security;

/// <summary>
/// F5 wallet surface (WAL-02, WAL-03, WAL-04, WAL-05B, WAL-07, WAL-08, WAL-09, WAL-10).
/// Routes follow the F5 contract sheet: <c>/api/v1/wallets/*</c> and <c>/api/v1/admin/wallets/*</c>.
/// </summary>
/// <remarks>
/// <para>Mock đọc <c>Seeds/wallet/*.json</c> qua <c>IGatewayDispatcher</c>; response bọc trong
/// <c>ApiResponse&lt;T&gt;</c> và có header <c>X-Gateway-Mode</c>. Ngoại lệ duy nhất: <c>POST vnpay/ipn</c>
/// (WAL-04) là webhook VNPay công khai, trả nguyên định dạng <c>{ RspCode, Message }</c> của VNPay.</para>
/// <para>Real chưa nối: contract gRPC của Wallet chưa tồn tại và <c>shop.proto</c> không được sửa,
/// nên <c>realCall</c> chỉ là placeholder không bao giờ được gọi trong Mock mode.</para>
/// </remarks>
[ApiController]
[Route("api/v1")]
[BackendTag("wallet")]
[EnableRateLimiting("api")]
public sealed class WalletController : ControllerBase
{
    private const string Service = "wallet";

    // Mock-only pass-through: in Mock mode GatewayDispatcher never invokes realCall, only the
    // parser. Struct round-trips the seed JSON object, so no wallet.proto is needed for Q2.
    private static readonly MessageParser<Struct> SeedParser = Struct.Parser;

    private readonly IGatewayDispatcher _dispatcher;

    public WalletController(IGatewayDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    private static AsyncUnaryCall<Struct> PendingRealCall(CallOptions _) =>
        throw new NotSupportedException("The wallet gRPC contract is not wired yet.");

    /// <summary>Lấy thông tin ví và số dư khả dụng/bị khóa của cá nhân (WAL-02)</summary>
    [HttpGet("wallets/me")]
    [Authorize(Policy = GatewayPolicies.Authenticated)]
    public Task<IResult> GetMyWallet()
    {
        return _dispatcher.DispatchAsync(
            HttpContext, Service, "wallet-me", PendingRealCall, SeedParser,
            successMessage: "Wallet retrieved successfully.");
    }

    /// <summary>Tạo yêu cầu nạp tiền qua VNPay và lấy URL thanh toán (WAL-03)</summary>
    [HttpPost("wallets/topups/vnpay")]
    [Authorize(Policy = GatewayPolicies.Authenticated)]
    public Task<IResult> InitiateVnPayTopup([FromBody] TopupVnPayRequest body)
    {
        if (body.Amount < 10000m)
            throw new GatewayInputException("Deposit amount must be at least 10,000 VND.", "Amount", "VAL_INVALID_AMOUNT");

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "wallet-topup", PendingRealCall, SeedParser,
            successMessage: "Top-up request created successfully.",
            successStatusCode: StatusCodes.Status201Created);
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

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "wallet-checkout-direct", PendingRealCall, SeedParser,
            successMessage: "Direct payment session created successfully.");
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

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "wallet-withdrawal", PendingRealCall, SeedParser,
            successMessage: "Withdrawal request created successfully.",
            successStatusCode: StatusCodes.Status201Created);
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

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "wallet-withdrawal-process", PendingRealCall, SeedParser,
            successMessage: "Withdrawal request processed successfully.");
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

        return _dispatcher.DispatchPagedAsync(
            HttpContext, Service, "wallet-transactions",
            page, limit, PendingRealCall, SeedParser);
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

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "wallet-adjustment", PendingRealCall, SeedParser,
            successMessage: "Wallet balance adjusted successfully.");
    }
}
