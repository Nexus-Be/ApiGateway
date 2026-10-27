using System.Text.Json;

namespace Nexus.Gateway.Dtos.Auction;

// HTTP request bodies for the Auction service (apicontract_Auction.xlsx).
// Fields are nullable so the Gateway can report "missing" and "invalid" separately.

public sealed record CreateAuctionRequestDto(
    string? ProductId,
    string? SkuId,
    decimal? StartPriceAmount,
    decimal? BidStepAmount,
    DateTimeOffset? ScheduledStartAt,
    DateTimeOffset? ScheduledEndAt);

public sealed record UpdateAuctionRequestDto(
    decimal? StartPriceAmount,
    decimal? BidStepAmount,
    DateTimeOffset? ScheduledStartAt,
    DateTimeOffset? ScheduledEndAt);

public sealed record CancelAuctionRequestDto(string? Reason);

public sealed record RecreateAuctionRequestDto(
    decimal? StartPriceAmount,
    decimal? BidStepAmount,
    DateTimeOffset? ScheduledStartAt,
    DateTimeOffset? ScheduledEndAt);

public sealed record ForceCancelAuctionRequestDto(string? Reason);

public sealed record UpdateAuctionConfigRequestDto(
    JsonElement? ConfigValue,
    string? Description);

public sealed record PlaceBidRequestDto(decimal? Amount);

// ============================================================================
// Response DTOs for Gateway Swagger / Documentation Contracts
// ============================================================================

public sealed class AuctionSummaryDto
{
    public string? AuctionId { get; set; }
    public string? ProductId { get; set; }
    public decimal CurrentPrice { get; set; }
    public string? Status { get; set; }
}

public sealed class AuctionDetailResponseDto
{
    public string? AuctionId { get; set; }
    public string? ProductId { get; set; }
    public decimal StartPrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public string? Status { get; set; }
}

public sealed class BidItemDto
{
    public string? BidId { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset BidAt { get; set; }
}

public sealed class PlaceBidResponseDto
{
    public string? BidId { get; set; }
    public string? Message { get; set; }
}

public sealed class MyBidItemDto
{
    public string? BidId { get; set; }
    public string? AuctionId { get; set; }
    public decimal Amount { get; set; }
}

public sealed class AuctionResultResponseDto
{
    public string? AuctionId { get; set; }
    public string? WinnerId { get; set; }
    public decimal WinningBid { get; set; }
    public string? Status { get; set; }
}

public sealed class AuctionCreatedResponseDto
{
    public string? AuctionId { get; set; }
    public string? Status { get; set; }
}

public sealed class AuctionUpdatedResponseDto
{
    public string? AuctionId { get; set; }
    public string? Message { get; set; }
}

public sealed class AuctionCancelResponseDto
{
    public string? AuctionId { get; set; }
    public string? Status { get; set; }
}

public sealed class AuctionRecreatedResponseDto
{
    public string? AuctionId { get; set; }
    public string? Status { get; set; }
}

public sealed class AuctionStatusHistoryResponseDto
{
    public List<object>? History { get; set; }
}

public sealed class AuctionForceCancelResponseDto
{
    public string? AuctionId { get; set; }
    public string? Status { get; set; }
}

public sealed class AuctionSagaResponseDto
{
    public string? SagaId { get; set; }
    public string? Status { get; set; }
}

public sealed class AuctionConfigListResponseDto
{
    public List<object>? Configs { get; set; }
}

public sealed class AuctionConfigResponseDto
{
    public string? Key { get; set; }
    public JsonElement? Value { get; set; }
}
