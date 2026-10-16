namespace Nexus.Gateway.Controllers;

using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.Gateway.Common;
using Nexus.Gateway.Dispatching;
using Nexus.Gateway.Dtos.Wallet;
using Nexus.Gateway.Responses;
using Nexus.Gateway.Security;
using ShoppingCart.Contracts.V1;

[ApiController]
[Route("api/v1/wallet")]
[BackendTag("wallet")]
[EnableRateLimiting("api")]
[Authorize("authenticated")]
public sealed class WalletController : ControllerBase
{
    private readonly IGatewayDispatcher _dispatcher;
    private readonly WalletApi.WalletApiClient _client;

    public WalletController(IGatewayDispatcher dispatcher, WalletApi.WalletApiClient client)
    {
        _dispatcher = dispatcher;
        _client = client;
    }

    [HttpGet("balance")]
    [ProducesResponseType(typeof(ApiResponse<Wallet>), StatusCodes.Status200OK)]
    public Task<IResult> GetBalance()
    {
        return _dispatcher.DispatchAsync(
            HttpContext,
            "wallet",
            "wallet-balance",
            o => _client.GetWalletAsync(HttpContext.ToUserRequest(), o),
            Wallet.Parser,
            successMessage: "Wallet retrieved successfully."
        );
    }

    [HttpPost("deposit")]
    [ProducesResponseType(typeof(ApiResponse<WalletTransaction>), StatusCodes.Status200OK)]
    public Task<IResult> Deposit([FromBody] WalletDepositRequestDto body)
    {
        if (!MoneyPrecision.TryParsePositive(body.Amount, out var amt))
            throw new GatewayInputException("amount phải là số dương hợp lệ", "Amount");

        return _dispatcher.DispatchAsync(
            HttpContext,
            "wallet",
            "wallet-deposit",
            o => _client.DepositAsync(new WalletDepositRequest
            {
                UserId = HttpContext.ToUserRequest().UserId,
                Amount = amt.ToString("0.00", CultureInfo.InvariantCulture),
                Currency = string.IsNullOrWhiteSpace(body.Currency) ? "VND" : body.Currency.Trim().ToUpperInvariant(),
                PaymentMethod = string.IsNullOrWhiteSpace(body.PaymentMethod) ? "BANK_TRANSFER" : body.PaymentMethod.Trim()
            }, o),
            WalletTransaction.Parser,
            successMessage: "Deposit processed successfully."
        );
    }

    [HttpPost("withdraw")]
    [ProducesResponseType(typeof(ApiResponse<WalletTransaction>), StatusCodes.Status200OK)]
    public Task<IResult> Withdraw([FromBody] WalletWithdrawRequestDto body)
    {
        if (!MoneyPrecision.TryParsePositive(body.Amount, out var amt))
            throw new GatewayInputException("amount phải là số dương hợp lệ", "Amount");
        ValidationHelpers.EnsureRequired(body.BankAccount, "BankAccount");

        return _dispatcher.DispatchAsync(
            HttpContext,
            "wallet",
            "wallet-withdraw",
            o => _client.WithdrawAsync(new WalletWithdrawRequest
            {
                UserId = HttpContext.ToUserRequest().UserId,
                Amount = amt.ToString("0.00", CultureInfo.InvariantCulture),
                Currency = string.IsNullOrWhiteSpace(body.Currency) ? "VND" : body.Currency.Trim().ToUpperInvariant(),
                BankAccount = body.BankAccount.Trim()
            }, o),
            WalletTransaction.Parser,
            successMessage: "Withdrawal processed successfully."
        );
    }

    [HttpPost("transfer")]
    [ProducesResponseType(typeof(ApiResponse<WalletTransaction>), StatusCodes.Status200OK)]
    public Task<IResult> Transfer([FromBody] WalletTransferRequestDto body)
    {
        if (!ValidationHelpers.GuidOk(body.RecipientUserId) || string.IsNullOrWhiteSpace(body.RecipientUserId))
            throw new GatewayInputException("recipientUserId phải là UUID hợp lệ", "RecipientUserId");
        if (!MoneyPrecision.TryParsePositive(body.Amount, out var amt))
            throw new GatewayInputException("amount phải là số dương hợp lệ", "Amount");

        var sender = HttpContext.ToUserRequest();
        if (Guid.TryParse(sender.UserId, out var senderId) &&
            Guid.TryParse(body.RecipientUserId, out var recipientId) &&
            senderId == recipientId)
            throw new GatewayInputException("Không thể tự chuyển tiền cho chính mình", "RecipientUserId");

        return _dispatcher.DispatchAsync(
            HttpContext,
            "wallet",
            "wallet-transfer",
            o => _client.TransferAsync(new WalletTransferRequest
            {
                SenderUserId = sender.UserId,
                RecipientUserId = body.RecipientUserId,
                Amount = amt.ToString("0.00", CultureInfo.InvariantCulture),
                Note = body.Note ?? ""
            }, o),
            WalletTransaction.Parser,
            successMessage: "Transfer processed successfully."
        );
    }

    [HttpGet("transactions")]
    [ProducesResponseType(typeof(ApiResponse<PagedData<WalletTransaction>>), StatusCodes.Status200OK)]
    public Task<IResult> ListTransactions([FromQuery] PaginationRequest pagination, [FromQuery] string? type = null)
    {
        pagination.EnsureValid();

        return _dispatcher.DispatchPagedAsync(
            HttpContext,
            "wallet",
            "wallet-transactions",
            pagination.Page,
            pagination.PageSize,
            o => _client.ListTransactionsAsync(new WalletTransactionsRequest
            {
                UserId = HttpContext.ToUserRequest().UserId,
                Page = pagination.Page,
                PageSize = pagination.PageSize,
                Type = type ?? ""
            }, o),
            WalletTransactionPage.Parser
        );
    }
}
