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
[Route("gateway/api/v1/wallet")]
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
        if (!decimal.TryParse(body.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var amt) || amt <= 0)
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "amount phải là số dương hợp lệ", field: "Amount"));

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
        if (!decimal.TryParse(body.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var amt) || amt <= 0)
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "amount phải là số dương hợp lệ", field: "Amount"));
        if (string.IsNullOrWhiteSpace(body.BankAccount))
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "bankAccount là bắt buộc", field: "BankAccount"));

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
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "recipientUserId phải là UUID hợp lệ", field: "RecipientUserId"));
        if (!decimal.TryParse(body.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var amt) || amt <= 0)
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "amount phải là số dương hợp lệ", field: "Amount"));

        var sender = HttpContext.ToUserRequest();
        if (string.Equals(sender.UserId, body.RecipientUserId, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "Không thể tự chuyển tiền cho chính mình", field: "RecipientUserId"));

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
    public Task<IResult> ListTransactions([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? type = null)
    {
        if (!ValidationHelpers.PageOk(page, pageSize))
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, ValidationHelpers.PageMessage, field: "Pagination"));

        return _dispatcher.DispatchPagedAsync(
            HttpContext,
            "wallet",
            "wallet-transactions",
            page,
            pageSize,
            o => _client.ListTransactionsAsync(new WalletTransactionsRequest
            {
                UserId = HttpContext.ToUserRequest().UserId,
                Page = page,
                PageSize = pageSize,
                Type = type ?? ""
            }, o),
            WalletTransactionPage.Parser
        );
    }
}
