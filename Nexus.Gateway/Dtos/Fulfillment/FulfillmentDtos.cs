namespace Nexus.Gateway.Dtos.Fulfillment;

// ============================================================
// FULFILLMENT REQUEST DTOs
// Aligned with FulfillmentController.cs endpoints (FUL-17..FUL-23)
// Excel: Fulfillment flows mentioned in Commerce section (F1)
// ============================================================

// ---------- REQUESTS ----------

/// <summary>FUL-17: PATCH /api/v1/shipments/{shipmentId}/tracking - update shipment tracking status</summary>
public sealed class UpdateShipmentTrackingRequest
{
    public string? Status { get; set; }       // PENDING|PICKED_UP|IN_TRANSIT|OUT_FOR_DELIVERY|DELIVERED|FAILED|RETURNED
    public string? Location { get; set; }
    public string? Description { get; set; }
    public string? Timestamp { get; set; }
}

/// <summary>FUL-17: POST /api/v1/shipments/webhook - webhook from carrier</summary>
public sealed class ShipmentWebhookRequest
{
    public string? TrackingNumber { get; set; }
    public string? Status { get; set; }
    public string? Location { get; set; }
    public string? Note { get; set; }
    public string? Timestamp { get; set; }
}

// ============================================================
// FULFILLMENT RESPONSE DTOs
// ============================================================

/// <summary>FUL-18: GET /api/v1/shipments/{shipmentId} - shipment detail and tracking events</summary>
public sealed class ShipmentDetailResponseDto
{
    public string? ShipmentId { get; set; }
    public string? OrderId { get; set; }
    public string? SubOrderId { get; set; }
    public string? TrackingNumber { get; set; }
    public string? CarrierCode { get; set; }
    public string? CarrierName { get; set; }
    public string? Status { get; set; }         // PENDING|PICKED_UP|IN_TRANSIT|OUT_FOR_DELIVERY|DELIVERED|FAILED|RETURNED
    public ShipmentAddressDto? OriginAddress { get; set; }
    public ShipmentAddressDto? DestinationAddress { get; set; }
    public decimal ShippingFee { get; set; }
    public string? EstimatedDeliveryAt { get; set; }
    public string? DeliveredAt { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
    public List<ShipmentEventItemDto>? Events { get; set; }
}

public sealed class ShipmentAddressDto
{
    public string? RecipientName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? AddressLine { get; set; }
    public string? Ward { get; set; }
    public string? District { get; set; }
    public string? Province { get; set; }
}

public sealed class ShipmentEventItemDto
{
    public string? EventId { get; set; }
    public string? Status { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
    public string? OccurredAt { get; set; }
}

/// <summary>FUL-18: GET /api/v1/shipments/order/{orderId} - list shipments by order</summary>
public sealed class ShipmentListItemDto
{
    public string? ShipmentId { get; set; }
    public string? SubOrderId { get; set; }
    public string? TrackingNumber { get; set; }
    public string? CarrierCode { get; set; }
    public string? Status { get; set; }
    public string? EstimatedDeliveryAt { get; set; }
    public string? DeliveredAt { get; set; }
    public string? CreatedAt { get; set; }
}

/// <summary>FUL-20: GET /api/v1/seller/shipment - list seller shipments</summary>
public sealed class SellerShipmentListItemDto
{
    public string? ShipmentId { get; set; }
    public string? OrderId { get; set; }
    public string? SubOrderId { get; set; }
    public string? TrackingNumber { get; set; }
    public string? CarrierCode { get; set; }
    public string? BuyerName { get; set; }
    public string? Status { get; set; }
    public decimal ShippingFee { get; set; }
    public string? EstimatedDeliveryAt { get; set; }
    public string? CreatedAt { get; set; }
}

/// <summary>FUL-17: PATCH /api/v1/shipments/{shipmentId}/tracking - 200 OK response</summary>
public sealed class ShipmentTrackingUpdatedResponseDto
{
    public string? ShipmentId { get; set; }
    public string? TrackingNumber { get; set; }
    public string? CurrentStatus { get => Status; set => Status = value; }
    public string? Status { get; set; }
    public string? Location { get; set; }
    public string? UpdatedAt { get; set; }
}

/// <summary>FUL-23: POST /api/v1/shipment/{shipmentId}/complete - confirm delivery</summary>
public sealed class ShipmentCompletedResponseDto
{
    public string? ShipmentId { get; set; }
    public string? Status { get; set; }       // DELIVERED
    public string? CompletedAt { get => DeliveredAt; set => DeliveredAt = value; }
    public string? DeliveredAt { get; set; }
    public string? OrderId { get; set; }
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
