namespace Nexus.Gateway.Dtos.Fulfillment;

public sealed record ShippingQuoteRequestDto(string SellerAddress, string BuyerAddress, int WeightGrams);

// Compatibility aliases
public sealed record ShippingQuoteBody(string SellerAddress, string BuyerAddress, int WeightGrams);
