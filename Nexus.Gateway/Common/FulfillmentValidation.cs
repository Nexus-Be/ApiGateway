namespace Nexus.Gateway.Common;

using System;
using System.Collections.Generic;

/// <summary>
/// Gateway-owned validation for the F4 fulfillment surface.
/// Matches real Fulfillment.Domain.Enums contracts.
/// </summary>
public static class FulfillmentValidation
{
    public static readonly string[] ShipmentStatuses =
        ["Draft", "ReadyToShip", "PickedUp", "InTransit", "OutForDelivery", "Delivered", "FailedAttempt", "Returning", "Returned", "Cancelled"];

    public static void EnsureOptionalShipmentStatus(string? status, string field = "status")
    {
        if (string.IsNullOrWhiteSpace(status)) return;
        EnumValidationHelper.EnsureOneOf(status, field, ShipmentStatuses);
    }

    public static void EnsureShipmentStatus(string? status, string field = "Status")
    {
        if (string.IsNullOrWhiteSpace(status))
            throw new GatewayInputException("Shipment status is required.", field, EnumValidationHelper.ErrorCode);
        EnumValidationHelper.EnsureOneOf(status, field, ShipmentStatuses);
    }
}
