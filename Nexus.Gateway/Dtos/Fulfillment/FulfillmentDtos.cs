namespace Nexus.Gateway.Dtos.Fulfillment;

// Request bodies for the F1 fulfillment surface (shipment tracking, completion, carrier webhook).
// Responses are served from Seeds/fulfillment/*.json while the gateway runs this feature in Mock mode.

public sealed class UpdateShipmentTrackingRequest
{
    public string? Status { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
    public string? Timestamp { get; set; }
}

public sealed class ShipmentWebhookRequest
{
    public string? TrackingNumber { get; set; }
    public string? Status { get; set; }
    public string? Location { get; set; }
    public string? Note { get; set; }
    public string? Timestamp { get; set; }
}
