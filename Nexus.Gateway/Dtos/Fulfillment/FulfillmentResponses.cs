namespace Nexus.Gateway.Dtos.Fulfillment;

public sealed record ShippingQuoteResponseDto(string Carrier, string Fee, int EstimatedDays);
public sealed record ShipmentTrackingResponseDto(string TrackingCode, string Status, string CurrentLocation);
