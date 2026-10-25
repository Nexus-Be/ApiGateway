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
