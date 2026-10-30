namespace Nexus.Gateway.Controllers;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.Gateway.Common;
using Nexus.Gateway.Dispatching;
using Nexus.Gateway.Dtos.Auction;
using Nexus.Gateway.Responses;
using Nexus.Gateway.Security;

[ApiController]
[Route("api/v1")]
[BackendTag("auction")]
[EnableRateLimiting("api")]
public sealed class AuctionController : GatewayBaseController
{
    private const string Service = "auction";

    public AuctionController(IGatewayDispatcher dispatcher)
        : base(dispatcher)
    {
    }

    /// <summary>Lists auctions with optional filtering and pagination.</summary>
    [HttpGet("auctions")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PagedData<AuctionSummaryDto>>), StatusCodes.Status200OK)]
    public Task<IResult> ListAuctions([FromQuery] PaginationRequest pagination, [FromQuery] string? status)
    {
        pagination.EnsureValid();
        AuctionValidation.EnsureStatus(status);

        return ForwardPagedAsync(
            Service,
            "/api/v1/auctions",
            pagination.Page,
            pagination.PageSize,
            HttpMethod.Get,
            mockSeed: "auctions",
            queryParams: new Dictionary<string, string?>
            {
                ["page"] = pagination.Page.ToString(),
                ["pageSize"] = pagination.PageSize.ToString(),
                ["status"] = status
            }
        );
    }

    /// <summary>Gets auction details by ID.</summary>
    [HttpGet("auctions/{auctionId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuctionDetailResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> GetAuction(string auctionId)
    {
        AuctionValidation.EnsureAuctionId(auctionId);

        return ForwardAsync(
            Service,
            $"/api/v1/auctions/{auctionId}",
            HttpMethod.Get,
            mockSeed: "auction",
            successMessage: "Auction retrieved successfully."
        );
    }

    /// <summary>Lists bids for a specific auction.</summary>
    [HttpGet("auctions/{auctionId}/bids")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PagedData<BidItemDto>>), StatusCodes.Status200OK)]
    public Task<IResult> ListAuctionBids(string auctionId, [FromQuery] PaginationRequest pagination)
    {
        AuctionValidation.EnsureAuctionId(auctionId);
        pagination.EnsureValid();

        return ForwardPagedAsync(
            Service,
            $"/api/v1/auctions/{auctionId}/bids",
            pagination.Page,
            pagination.PageSize,
            HttpMethod.Get,
            mockSeed: "auction-bids",
            queryParams: new Dictionary<string, string?>
            {
                ["page"] = pagination.Page.ToString(),
                ["pageSize"] = pagination.PageSize.ToString()
            }
        );
    }

    /// <summary>Places a bid on an auction.</summary>
    [HttpPost("auctions/{auctionId}/bids")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<PlaceBidResponseDto>), StatusCodes.Status201Created)]
    public Task<IResult> PlaceBid(
        string auctionId,
        [FromBody] PlaceBidRequestDto body,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromHeader(Name = "If-Match")] string? ifMatch)
    {
        AuctionValidation.EnsureAuctionId(auctionId);
        AuctionValidation.EnsureBidHeaders(idempotencyKey, ifMatch);
        AuctionValidation.EnsureBidAmount(body.Amount);

        return ForwardAsync(
            Service,
            $"/api/v1/auctions/{auctionId}/bids",
            HttpMethod.Post,
            body: body,
            mockSeed: "auction-bid-place",
            successMessage: "Bid placed successfully.",
            successStatusCode: StatusCodes.Status201Created
        );
    }

    /// <summary>Lists bids placed by current user.</summary>
    [HttpGet("me/bids")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<PagedData<MyBidItemDto>>), StatusCodes.Status200OK)]
    public Task<IResult> ListMyBids([FromQuery] PaginationRequest pagination, [FromQuery] string? status)
    {
        pagination.EnsureValid();
        AuctionValidation.EnsureStatus(status);

        return ForwardPagedAsync(
            Service,
            "/api/v1/me/bids",
            pagination.Page,
            pagination.PageSize,
            HttpMethod.Get,
            mockSeed: "me-bids",
            queryParams: new Dictionary<string, string?>
            {
                ["page"] = pagination.Page.ToString(),
                ["pageSize"] = pagination.PageSize.ToString(),
                ["status"] = status
            }
        );
    }

    /// <summary>Gets results of an auction.</summary>
    [HttpGet("auctions/{auctionId}/result")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<AuctionResultResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> GetAuctionResult(string auctionId)
    {
        AuctionValidation.EnsureAuctionId(auctionId);

        return ForwardAsync(
            Service,
            $"/api/v1/auctions/{auctionId}/result",
            HttpMethod.Get,
            mockSeed: "auction-result",
            successMessage: "Auction result retrieved successfully."
        );
    }

    /// <summary>Creates a new auction.</summary>
    [HttpPost("auctions")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<AuctionCreatedResponseDto>), StatusCodes.Status201Created)]
    public Task<IResult> CreateAuction([FromBody] CreateAuctionRequestDto body)
    {
        AuctionValidation.EnsureCreate(body);

        return ForwardAsync(
            Service,
            "/api/v1/auctions",
            HttpMethod.Post,
            body: body,
            mockSeed: "auction-create",
            successMessage: "Auction scheduled successfully.",
            successStatusCode: StatusCodes.Status201Created
        );
    }

    /// <summary>Updates an auction before it starts.</summary>
    [HttpPut("auctions/{auctionId}")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<AuctionUpdatedResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> UpdateAuction(string auctionId, [FromBody] UpdateAuctionRequestDto body)
    {
        AuctionValidation.EnsureAuctionId(auctionId);
        AuctionValidation.EnsureUpdate(body);

        return ForwardAsync(
            Service,
            $"/api/v1/auctions/{auctionId}",
            HttpMethod.Put,
            body: body,
            mockSeed: "auction-update",
            successMessage: "Auction updated successfully."
        );
    }

    /// <summary>Cancels an auction.</summary>
    [HttpPost("auctions/{auctionId}/cancel")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<AuctionCancelResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> CancelAuction(
        string auctionId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CancelAuctionRequestDto? body)
    {
        AuctionValidation.EnsureAuctionId(auctionId);
        AuctionValidation.EnsureOptionalReason(body?.Reason);

        return ForwardAsync(
            Service,
            $"/api/v1/auctions/{auctionId}/cancel",
            HttpMethod.Post,
            body: body,
            mockSeed: "auction-cancel",
            successMessage: "Auction cancelled successfully."
        );
    }

    /// <summary>Recreates an auction from a failed session.</summary>
    [HttpPost("auctions/{auctionId}/recreate")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<AuctionRecreatedResponseDto>), StatusCodes.Status201Created)]
    public Task<IResult> RecreateAuction(string auctionId, [FromBody] RecreateAuctionRequestDto body)
    {
        AuctionValidation.EnsureAuctionId(auctionId);
        AuctionValidation.EnsureRecreate(body);

        return ForwardAsync(
            Service,
            $"/api/v1/auctions/{auctionId}/recreate",
            HttpMethod.Post,
            body: body,
            mockSeed: "auction-recreate",
            successMessage: "Auction recreated successfully.",
            successStatusCode: StatusCodes.Status201Created
        );
    }

    /// <summary>Lists auctions created by the seller.</summary>
    [HttpGet("seller/auctions")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<PagedData<AuctionSummaryDto>>), StatusCodes.Status200OK)]
    public Task<IResult> ListSellerAuctions([FromQuery] PaginationRequest pagination, [FromQuery] string? status)
    {
        pagination.EnsureValid();
        AuctionValidation.EnsureStatus(status);

        return ForwardPagedAsync(
            Service,
            "/api/v1/seller/auctions",
            pagination.Page,
            pagination.PageSize,
            HttpMethod.Get,
            mockSeed: "seller-auctions",
            queryParams: new Dictionary<string, string?>
            {
                ["page"] = pagination.Page.ToString(),
                ["pageSize"] = pagination.PageSize.ToString(),
                ["status"] = status
            }
        );
    }

    /// <summary>Gets status change history of an auction.</summary>
    [HttpGet("auctions/{auctionId}/status-history")]
    [Authorize(GatewayPolicies.Staff)]
    [ProducesResponseType(typeof(ApiResponse<AuctionStatusHistoryResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> GetAuctionStatusHistory(string auctionId)
    {
        AuctionValidation.EnsureAuctionId(auctionId);

        return ForwardAsync(
            Service,
            $"/api/v1/auctions/{auctionId}/status-history",
            HttpMethod.Get,
            mockSeed: "auction-status-history",
            successMessage: "Auction status history retrieved successfully."
        );
    }

    /// <summary>Forces cancellation of an auction by admin.</summary>
    [HttpPost("admin/auctions/{auctionId}/force-cancel")]
    [Authorize(GatewayPolicies.Admin)]
    [ProducesResponseType(typeof(ApiResponse<AuctionForceCancelResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> ForceCancelAuction(string auctionId, [FromBody] ForceCancelAuctionRequestDto body)
    {
        AuctionValidation.EnsureAuctionId(auctionId);
        AuctionValidation.EnsureRequiredReason(body.Reason);

        return ForwardAsync(
            Service,
            $"/api/v1/admin/auctions/{auctionId}/force-cancel",
            HttpMethod.Post,
            body: body,
            mockSeed: "auction-force-cancel",
            successMessage: "Auction cancelled by administrator successfully."
        );
    }

    /// <summary>Gets order tracking saga status for an auction.</summary>
    [HttpGet("admin/auctions/{auctionId}/saga")]
    [Authorize(GatewayPolicies.Staff)]
    [ProducesResponseType(typeof(ApiResponse<AuctionSagaResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> GetAuctionSaga(string auctionId)
    {
        AuctionValidation.EnsureAuctionId(auctionId);

        return ForwardAsync(
            Service,
            $"/api/v1/admin/auctions/{auctionId}/saga",
            HttpMethod.Get,
            mockSeed: "auction-saga",
            successMessage: "Auction saga retrieved successfully."
        );
    }

    /// <summary>Lists active auction configuration rules.</summary>
    [HttpGet("admin/auction-configs")]
    [Authorize(GatewayPolicies.Admin)]
    [ProducesResponseType(typeof(ApiResponse<AuctionConfigListResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> ListAuctionConfigs()
    {
        return ForwardAsync(
            Service,
            "/api/v1/admin/auction-configs",
            HttpMethod.Get,
            mockSeed: "auction-configs",
            successMessage: "Auction configs retrieved successfully."
        );
    }

    /// <summary>Updates auction configuration settings.</summary>
    [HttpPut("admin/auction-configs/{key}")]
    [Authorize(GatewayPolicies.Admin)]
    [ProducesResponseType(typeof(ApiResponse<AuctionConfigResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> UpdateAuctionConfig(string key, [FromBody] UpdateAuctionConfigRequestDto body)
    {
        AuctionValidation.EnsureConfig(key, body.ConfigValue);

        return ForwardAsync(
            Service,
            $"/api/v1/admin/auction-configs/{key}",
            HttpMethod.Put,
            body: body,
            mockSeed: "auction-config-update",
            successMessage: "Auction config updated successfully."
        );
    }

    /// <summary>Retrieves auction status by product ID internally.</summary>
    [HttpGet("/internal/v1/auctions/status")]
    [AllowAnonymous]
    public Task<IResult> GetInternalAuctionStatus([FromQuery] Guid productId)
    {
        var query = new Dictionary<string, string?>
        {
            ["productId"] = productId.ToString()
        };

        return ForwardAsync(
            Service,
            "/internal/v1/auctions/status",
            HttpMethod.Get,
            mockSeed: "internal-auction-status",
            successMessage: "Auction status retrieved successfully.",
            queryParams: query
        );
    }

    /// <summary>Retrieves auction summary by auction ID internally.</summary>
    [HttpGet("/internal/v1/auctions/{auctionId}/summary")]
    [AllowAnonymous]
    public Task<IResult> GetInternalAuctionSummary([FromRoute] string auctionId)
    {
        return ForwardAsync(
            Service,
            $"/internal/v1/auctions/{auctionId}/summary",
            HttpMethod.Get,
            mockSeed: "internal-auction-summary",
            successMessage: "Auction summary retrieved successfully."
        );
    }
}
