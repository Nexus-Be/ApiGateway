namespace Nexus.Gateway.Dtos.Auction;

public sealed record CreateAuctionRequestDto(string ProductId, string StartingPrice, string StepPrice, string StartTime, string EndTime, string Title);
public sealed record PlaceBidRequestDto(string Amount);

// Compatibility aliases
public sealed record CreateAuctionBody(string ProductId, string StartingPrice, string StepPrice, string StartTime, string EndTime, string Title);
public sealed record PlaceBidBody(string Amount);
