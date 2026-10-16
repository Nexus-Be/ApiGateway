namespace Nexus.Gateway.Dtos.Auction;

public sealed record AuctionResponseDto(string Id, string ProductId, string SellerId, string StartingPrice, string CurrentPrice, string Status);
public sealed record BidResponseDto(string Id, string AuctionId, string UserId, string Amount, string CreatedAt);
