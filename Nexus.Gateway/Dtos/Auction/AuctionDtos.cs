namespace Nexus.Gateway.Dtos.Auction;

using System.Text.Json;

// ============================================================
// AUCTION REQUEST DTOs — aligned with flow_mock_excel.xlsx F3
// Names kept compatible with AuctionController.cs
// ============================================================

// ---------- REQUESTS ----------

/// <summary>AUC-10: POST /api/v1/auctions - seller creates auction</summary>
public sealed class CreateAuctionRequestDto
{
    public string? ProductId { get; set; }
    public string? SkuId { get; set; }
    public decimal? StartPriceAmount { get; set; }
    public decimal? BidStepAmount { get; set; }
    public DateTimeOffset? ScheduledStartAt { get; set; }
    public DateTimeOffset? ScheduledEndAt { get; set; }
}

/// <summary>AUC-11: PUT /api/v1/auctions/{auctionId} - seller updates auction before start</summary>
public sealed class UpdateAuctionRequestDto
{
    public decimal? StartPriceAmount { get; set; }
    public decimal? BidStepAmount { get; set; }
    public DateTimeOffset? ScheduledStartAt { get; set; }
    public DateTimeOffset? ScheduledEndAt { get; set; }
}

/// <summary>AUC-12: POST /api/v1/auctions/{auctionId}/cancel - seller cancels auction</summary>
public sealed class CancelAuctionRequestDto
{
    public string? Reason { get; set; }
}

/// <summary>AUC-13: POST /api/v1/auctions/{auctionId}/recreate - recreate auction from failed session</summary>
public sealed class RecreateAuctionRequestDto
{
    public decimal? StartPriceAmount { get; set; }
    public decimal? BidStepAmount { get; set; }
    public DateTimeOffset? ScheduledStartAt { get; set; }
    public DateTimeOffset? ScheduledEndAt { get; set; }
}

/// <summary>AUC-15: POST /api/v1/admin/auctions/{auctionId}/force-cancel - admin forces auction cancellation</summary>
public sealed class ForceCancelAuctionRequestDto
{
    public string? Reason { get; set; }
}

/// <summary>AUC-17: PUT /api/v1/admin/auction-configs/{key} - update auction configuration</summary>
public sealed class UpdateAuctionConfigRequestDto
{
    public JsonElement? ConfigValue { get; set; }
    public string? Description { get; set; }
}

/// <summary>AUC-07: POST /api/v1/auctions/{auctionId}/bids - buyer places a bid</summary>
public sealed class PlaceBidRequestDto
{
    public decimal Amount { get; set; }
}

// ============================================================
// AUCTION RESPONSE DTOs — aligned with flow_mock_excel.xlsx F3
// ============================================================

// ---------- AUCTION LIST (AUC-01 / AUC-14) ----------

/// <summary>GET /api/v1/auctions — list item; also used for AUC-14 seller list.
/// Controller uses: AuctionSummaryDto</summary>
public sealed class AuctionSummaryDto
{
    public string? AuctionId { get; set; }
    public string? ProductId { get; set; }
    public string? SkuId { get; set; }
    public string? SellerId { get; set; }
    public string? ProductName { get; set; }
    public string? PrimaryImageUrl { get; set; }
    public string? Status { get; set; } // DRAFT|SCHEDULED|RUNNING|ENDED|SETTLED|FAILED|CANCELLED
    public decimal? StartPriceAmount { get; set; }
    public decimal? CurrentPriceAmount { get; set; }
    public int BidCount { get; set; }
    public string? ScheduledStartAt { get; set; }
    public string? ScheduledEndAt { get; set; }
    public string? CreatedAt { get; set; }
}

// ---------- AUCTION DETAIL (AUC-02) ----------

/// <summary>GET /api/v1/auctions/{auctionId} — full detail</summary>
public sealed class AuctionDetailResponseDto
{
    public string? AuctionId { get; set; }
    public string? ProductId { get; set; }
    public string? SkuId { get; set; }
    public string? SellerId { get; set; }
    public string? ProductName { get; set; }
    public string? Description { get; set; }
    public List<AuctionProductImageDto>? Images { get; set; }
    public object? Specifications { get; set; }
    public string? Status { get; set; }
    public decimal StartPriceAmount { get; set; }
    public decimal BidStepAmount { get; set; }
    public decimal? CurrentPriceAmount { get; set; }
    public int BidCount { get; set; }
    public string? LeadingBidderDisplayName { get; set; }
    public string? ScheduledStartAt { get; set; }
    public string? OriginalEndAt { get; set; }
    public string? ScheduledEndAt { get; set; }
    public string? StartedAt { get; set; }
    public string? EndedAt { get; set; }
    public int ExtensionCount { get; set; }
    public int MaxExtensionCount { get; set; }
    public int AntiSnipingExtensionMinutes { get; set; }
    public int MinReputationToBid { get; set; }
    public string? RecreatedFromAuctionId { get; set; }
    public string? CreatedAt { get; set; }
}

public sealed class AuctionProductImageDto
{
    public string? Url { get; set; }
    public bool IsPrimary { get; set; }
}

// ---------- AUCTION MUTATIONS ----------

/// <summary>AUC-10: POST /api/v1/auctions — 201 Created</summary>
public sealed class AuctionCreatedResponseDto
{
    public string? AuctionId { get; set; }
    public string? ProductId { get; set; }
    public string? SkuId { get; set; }
    public string? ProductName { get; set; }
    public string? Status { get; set; }
    public decimal StartPriceAmount { get; set; }
    public decimal BidStepAmount { get; set; }
    public string? ScheduledStartAt { get; set; }
    public string? ScheduledEndAt { get; set; }
    public int MinReputationToBid { get; set; }
    public int AntiSnipingExtensionMinutes { get; set; }
    public int MaxExtensionCount { get; set; }
    public string? CreatedAt { get; set; }
}

/// <summary>AUC-11: PUT /api/v1/auctions/{auctionId} — 200 OK</summary>
public sealed class AuctionUpdatedResponseDto
{
    public string? AuctionId { get; set; }
    public string? Status { get; set; }
    public decimal StartPriceAmount { get; set; }
    public decimal BidStepAmount { get; set; }
    public string? ScheduledStartAt { get; set; }
    public string? ScheduledEndAt { get; set; }
    public string? UpdatedAt { get; set; }
}

/// <summary>AUC-12: POST /api/v1/auctions/{auctionId}/cancel — 201 Created
/// Controller uses: AuctionCancelResponseDto</summary>
public sealed class AuctionCancelResponseDto
{
    public string? AuctionId { get; set; }
    public string? FromStatus { get; set; }
    public string? Status { get; set; }
    public string? Reason { get; set; }
    public string? CancelledAt { get; set; }
}

/// <summary>AUC-13: POST /api/v1/auctions/{auctionId}/recreate — 201 Created</summary>
public sealed class AuctionRecreatedResponseDto
{
    public string? AuctionId { get; set; }
    public string? RecreatedFromAuctionId { get; set; }
    public string? ProductId { get; set; }
    public string? SkuId { get; set; }
    public string? Status { get; set; }
    public decimal StartPriceAmount { get; set; }
    public decimal BidStepAmount { get; set; }
    public string? ScheduledStartAt { get; set; }
    public string? ScheduledEndAt { get; set; }
    public string? CreatedAt { get; set; }
}

/// <summary>AUC-15: POST /api/v1/admin/auctions/{auctionId}/force-cancel — 201 Created
/// Controller uses: AuctionForceCancelResponseDto</summary>
public sealed class AuctionForceCancelResponseDto
{
    public string? AuctionId { get; set; }
    public string? FromStatus { get; set; }
    public string? Status { get; set; }
    public string? Reason { get; set; }
    public string? CancelledBy { get; set; }
    public string? CancelledAt { get; set; }
}

/// <summary>AUC-16: GET /api/v1/admin/auction-configs — wrapper
/// Controller uses: AuctionConfigListResponseDto</summary>
public sealed class AuctionConfigListResponseDto
{
    public List<AuctionConfigItemDto>? Items { get; set; }
    public PaginationDto? Pagination { get; set; }
}

/// <summary>AUC-16 config list item</summary>
public sealed class AuctionConfigItemDto
{
    public string? ConfigKey { get; set; }
    public JsonElement? ConfigValue { get; set; }
    public string? Description { get; set; }
    public int Version { get; set; }
    public string? UpdatedBy { get; set; }
    public string? UpdatedAt { get; set; }
}

/// <summary>AUC-17: PUT /api/v1/admin/auction-configs/{key} — 200 OK
/// Controller uses: AuctionConfigResponseDto</summary>
public sealed class AuctionConfigResponseDto
{
    public string? ConfigKey { get; set; }
    public JsonElement? ConfigValue { get; set; }
    public string? Description { get; set; }
    public int Version { get; set; }
    public bool IsActive { get; set; }
    public string? UpdatedBy { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
}

/// <summary>AUC-18: GET /api/v1/auctions/{auctionId}/status-history — wrapper
/// Controller uses: AuctionStatusHistoryResponseDto</summary>
public sealed class AuctionStatusHistoryResponseDto
{
    public List<AuctionStatusHistoryItemDto>? Items { get; set; }
    public PaginationDto? Pagination { get; set; }
}

public sealed class AuctionStatusHistoryItemDto
{
    public string? Id { get; set; }
    public string? FromStatus { get; set; }
    public string? ToStatus { get; set; }
    public string? Reason { get; set; }
    public string? ChangedBy { get; set; }
    public string? ChangedAt { get; set; }
}

/// <summary>AUC-19: GET /api/v1/admin/auctions/{auctionId}/saga — 200 OK</summary>
public sealed class AuctionSagaResponseDto
{
    public string? AuctionId { get; set; }
    public string? SagaType { get; set; }
    public string? CurrentState { get; set; }
    public object? StateData { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
}

/// <summary>AUC-07: POST /api/v1/auctions/{auctionId}/bids — 201 Created</summary>
/// <summary>Row 143: POST /api/v1/auctions/{auctionId}/bids</summary>
public sealed class PlaceBidResponseDto
{
    public string? BidId { get; set; }
    public string? AuctionId { get; set; }
    public string? BidderId { get; set; }
    public string? BidderDisplayName { get; set; }
    public decimal Amount { get; set; }
    public string? BidAt { get; set; }
    public bool IsLeading { get; set; }
    public decimal CurrentPriceAmount { get; set; }
    public int BidCount { get; set; }
    public string? NewScheduledEndAt { get; set; }
    public bool Extended { get; set; }

    // Backwards-compatible aliases
    public int BidSeq { get; set; }
    public string? PlacedAt { get => BidAt; set => BidAt = value; }
    public string? ScheduledEndAt { get => NewScheduledEndAt; set => NewScheduledEndAt = value; }
    public int ExtensionCount { get => Extended ? 1 : 0; set => Extended = value > 0; }
}

/// <summary>AUC-03: GET /api/v1/auctions/{auctionId}/bids — list item
/// Controller uses: BidItemDto</summary>
public sealed class BidItemDto
{
    public string? BidId { get; set; }
    public int SequenceNo { get; set; }
    public string? BidderId { get; set; }
    public string? BidderDisplayName { get; set; }
    public decimal Amount { get; set; }
    public string? PlacedAt { get; set; }
    public bool IsOutbid { get; set; }
    public bool IsWinning { get; set; }
    public bool IsInvalidated { get; set; }
}

/// <summary>AUC-08: GET /api/v1/me/bids — list item (Buyer's participated auctions)
/// Controller uses: MyBidItemDto</summary>
public sealed class MyBidItemDto
{
    public string? AuctionId { get; set; }
    public string? ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? PrimaryImageUrl { get; set; }
    public string? AuctionStatus { get; set; }
    public decimal MyHighestBidAmount { get; set; }
    public decimal? CurrentPriceAmount { get; set; }
    public bool IsLeading { get; set; }
    public string? EndsAt { get; set; }
    public string? LastBidAt { get; set; }
}

/// <summary>AUC-09: GET /api/v1/auctions/{auctionId}/result — 200 OK</summary>
public sealed class AuctionResultResponseDto
{
    public string? AuctionId { get; set; }
    public string? Status { get; set; }
    public bool HasWinner { get; set; }
    public string? WinnerId { get; set; }
    public string? WinnerDisplayName { get; set; }
    public string? SellerId { get; set; }
    public string? WinningBidId { get; set; }
    public decimal? FinalPriceAmount { get; set; }
    public string? DeterminedAt { get; set; }
    public string? OrderId { get; set; }
    public string? PaymentDeadlineAt { get; set; }
    public string? PaidAt { get; set; }
}

/// <summary>AUC-34: GET /internal/v1/auctions/status — product auction status</summary>
public sealed class AuctionProductStatusResponseDto
{
    public string? ProductId { get; set; }
    public string? AuctionId { get; set; }
    public string? Status { get; set; }
    public bool IsOpen { get; set; }
    public bool IsSold { get; set; }
}

/// <summary>AUC-35: GET /internal/v1/auctions/{auctionId}/summary</summary>
public sealed class AuctionSummaryResponseDto
{
    public string? AuctionId { get; set; }
    public string? ProductId { get; set; }
    public string? SellerId { get; set; }
    public string? Status { get; set; }
    public decimal StartingPriceAmount { get; set; }
    public decimal BidStepAmount { get; set; }
    public decimal? CurrentPriceAmount { get; set; }
    public string? CurrentLeadingBidderId { get; set; }
    public string? CurrentLeadingBidderMasked { get; set; }
    public int BidCount { get; set; }
    public string? StartsAt { get; set; }
    public string? EndsAt { get; set; }
    public int ExtensionCount { get; set; }
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
