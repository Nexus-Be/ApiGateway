namespace Nexus.Gateway.Controllers;

using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.Gateway.Common;
using Nexus.Gateway.Dispatching;
using Nexus.Gateway.Dtos.Auction;
using Nexus.Gateway.Responses;
using Nexus.Gateway.Security;
using ShoppingCart.Contracts.V1;

[ApiController]
[Route("gateway/api/v1/auctions")]
[BackendTag("auction")]
[EnableRateLimiting("api")]
public sealed class AuctionController : ControllerBase
{
    private readonly IGatewayDispatcher _dispatcher;
    private readonly AuctionApi.AuctionApiClient _client;

    public AuctionController(IGatewayDispatcher dispatcher, AuctionApi.AuctionApiClient client)
    {
        _dispatcher = dispatcher;
        _client = client;
    }

    [HttpGet("")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PagedData<Auction>>), StatusCodes.Status200OK)]
    public Task<IResult> ListAuctions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? sellerId = null)
    {
        if (!ValidationHelpers.PageOk(page, pageSize))
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, ValidationHelpers.PageMessage, field: "Pagination"));

        return _dispatcher.DispatchPagedAsync(
            HttpContext,
            "auction",
            "auctions",
            page,
            pageSize,
            o => _client.ListAuctionsAsync(new AuctionQuery
            {
                Page = page,
                PageSize = pageSize,
                Status = status ?? "",
                SellerId = sellerId ?? ""
            }, o),
            AuctionPage.Parser
        );
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<Auction>), StatusCodes.Status200OK)]
    public Task<IResult> GetAuction([FromRoute] Guid id)
    {
        return _dispatcher.DispatchAsync(
            HttpContext,
            "auction",
            "auction",
            o => _client.GetAuctionAsync(new IdRequest { Id = id.ToString() }, o),
            Auction.Parser,
            successMessage: "Auction retrieved successfully."
        );
    }

    [HttpPost("")]
    [Authorize("seller")]
    [ProducesResponseType(typeof(ApiResponse<Auction>), StatusCodes.Status201Created)]
    public Task<IResult> CreateAuction([FromBody] CreateAuctionRequestDto body)
    {
        if (!Guid.TryParse(body.ProductId, out _))
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "productId phải là UUID", field: "ProductId"));
        if (string.IsNullOrWhiteSpace(body.Title))
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "title là bắt buộc", field: "Title"));
        if (!decimal.TryParse(body.StartingPrice, NumberStyles.Number, CultureInfo.InvariantCulture, out var sp) || sp <= 0)
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "startingPrice phải là số > 0", field: "StartingPrice"));
        if (!decimal.TryParse(body.StepPrice, NumberStyles.Number, CultureInfo.InvariantCulture, out var stp) || stp <= 0)
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "stepPrice phải là số > 0", field: "StepPrice"));

        return _dispatcher.DispatchAsync(
            HttpContext,
            "auction",
            "auction-create",
            o => _client.CreateAuctionAsync(new CreateAuctionRequest
            {
                ProductId = body.ProductId,
                SellerId = HttpContext.ToUserRequest().UserId,
                StartingPrice = sp.ToString("0.00", CultureInfo.InvariantCulture),
                StepPrice = stp.ToString("0.00", CultureInfo.InvariantCulture),
                StartTime = body.StartTime ?? "",
                EndTime = body.EndTime ?? "",
                Title = body.Title
            }, o),
            Auction.Parser,
            successMessage: "Auction created successfully.",
            successStatusCode: StatusCodes.Status201Created
        );
    }

    [HttpPost("{id:guid}/bids")]
    [Authorize("authenticated")]
    [ProducesResponseType(typeof(ApiResponse<Bid>), StatusCodes.Status201Created)]
    public Task<IResult> PlaceBid([FromRoute] Guid id, [FromBody] PlaceBidRequestDto body)
    {
        if (!decimal.TryParse(body.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "amount phải là số > 0", field: "Amount"));

        return _dispatcher.DispatchAsync(
            HttpContext,
            "auction",
            "bid-place",
            o => _client.PlaceBidAsync(new PlaceBidRequest
            {
                AuctionId = id.ToString(),
                UserId = HttpContext.ToUserRequest().UserId,
                Amount = amount.ToString("0.00", CultureInfo.InvariantCulture)
            }, o),
            Bid.Parser,
            successMessage: "Bid placed successfully.",
            successStatusCode: StatusCodes.Status201Created
        );
    }

    [HttpGet("{id:guid}/bids")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<BidPage>), StatusCodes.Status200OK)]
    public Task<IResult> ListBids([FromRoute] Guid id)
    {
        return _dispatcher.DispatchAsync(
            HttpContext,
            "auction",
            "bids",
            o => _client.ListBidsAsync(new IdRequest { Id = id.ToString() }, o),
            BidPage.Parser,
            successMessage: "Bids retrieved successfully."
        );
    }
}
