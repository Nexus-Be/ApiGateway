namespace Nexus.Gateway.Controllers;

using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.Gateway.Common;
using Nexus.Gateway.Dispatching;
using Nexus.Gateway.Dtos.Commerce;
using Nexus.Gateway.Dtos.Fulfillment;
using Nexus.Gateway.Responses;
using Nexus.Gateway.Security;

[ApiController]
[Route("api/v1")]
[BackendTag("fulfillment")]
[EnableRateLimiting("api")]
public sealed class FulfillmentController : GatewayBaseController
{
    private const string Service = "fulfillment";

    public FulfillmentController(IGatewayDispatcher dispatcher) : base(dispatcher)
    {
    }

    /// <summary>Gets shipment details with tracking history.</summary>
    [HttpGet("shipments/{shipmentId}")]
    [Authorize(GatewayPolicies.Customer)]
    [ProducesResponseType(typeof(ApiResponse<ShipmentDetailResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<IResult> GetShipment([FromRoute] string shipmentId)
    {
        if (string.IsNullOrWhiteSpace(shipmentId))
            throw new GatewayInputException("Shipment ID is required.", "ShipmentId");

        return ForwardAsync(
            Service,
            $"/api/v1/shipments/{shipmentId}",
            HttpMethod.Get,
            mockSeed: "shipment-get"
        );
    }

    /// <summary>Lists shipments associated with an order.</summary>
    [HttpGet("shipments/order/{orderId}")]
    [Authorize(GatewayPolicies.Customer)]
    [ProducesResponseType(typeof(ApiResponse<List<ShipmentListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<IResult> GetShipmentsByOrder([FromRoute] string orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            throw new GatewayInputException("Order ID is required.", "OrderId");

        return ForwardAsync(
            Service,
            $"/api/v1/shipments/order/{orderId}",
            HttpMethod.Get,
            mockSeed: "shipments-by-order"
        );
    }

    /// <summary>Lists seller shipments filtered by status.</summary>
    [HttpGet("seller/shipment")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<PagedData<SellerShipmentListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<IResult> ListSellerShipments([FromQuery] PaginationRequest pagination, [FromQuery] string? status)
    {
        pagination.EnsureValid();
        FulfillmentValidation.EnsureOptionalShipmentStatus(status);

        var query = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(status)) query["status"] = status;

        return ForwardPagedAsync(
            Service,
            "/api/v1/seller/shipment",
            pagination.Page,
            pagination.PageSize,
            HttpMethod.Get,
            mockSeed: "seller-shipments",
            queryParams: query
        );
    }

    /// <summary>Searches seller shipments by keyword and date range.</summary>
    [HttpGet("seller/shipment/search")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<PagedData<SellerShipmentListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<IResult> SearchSellerShipments(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] string? keyword,
        [FromQuery] string? status,
        [FromQuery] string? fromDate,
        [FromQuery] string? toDate)
    {
        pagination.EnsureValid();
        FulfillmentValidation.EnsureOptionalShipmentStatus(status);

        var query = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(keyword)) query["keyword"] = keyword;
        if (!string.IsNullOrWhiteSpace(status)) query["status"] = status;
        if (!string.IsNullOrWhiteSpace(fromDate)) query["fromDate"] = fromDate;
        if (!string.IsNullOrWhiteSpace(toDate)) query["toDate"] = toDate;

        return ForwardPagedAsync(
            Service,
            "/api/v1/seller/shipment/search",
            pagination.Page,
            pagination.PageSize,
            HttpMethod.Get,
            mockSeed: "seller-shipments-search",
            queryParams: query
        );
    }

    /// <summary>Gets seller shipment details by shipment ID.</summary>
    [HttpGet("seller/shipment/{shipmentId}")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<ShipmentDetailResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<IResult> GetSellerShipment([FromRoute] string shipmentId)
    {
        if (string.IsNullOrWhiteSpace(shipmentId))
            throw new GatewayInputException("Shipment ID is required.", "ShipmentId");

        return ForwardAsync(
            Service,
            $"/api/v1/seller/shipment/{shipmentId}",
            HttpMethod.Get,
            mockSeed: "seller-shipment-get"
        );
    }

    /// <summary>Updates shipment tracking status from carrier.</summary>
    [HttpPatch("shipments/{shipmentId}/tracking")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<ShipmentTrackingUpdatedResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<IResult> UpdateShipmentTracking([FromRoute] string shipmentId, [FromBody] UpdateShipmentTrackingRequest body)
    {
        if (string.IsNullOrWhiteSpace(shipmentId))
            throw new GatewayInputException("Shipment ID is required.", "ShipmentId");
        FulfillmentValidation.EnsureShipmentStatus(body.Status);
        if (string.IsNullOrWhiteSpace(body.Location))
            throw new GatewayInputException("Location is required.", "Location");

        return ForwardAsync(
            Service,
            $"/api/v1/shipments/{shipmentId}/tracking",
            HttpMethod.Patch,
            body: body,
            mockSeed: "shipment-tracking-update"
        );
    }

    /// <summary>Confirms shipment delivery by customer.</summary>
    [HttpPost("shipment/{shipmentId}/complete")]
    [Authorize(GatewayPolicies.Customer)]
    [ProducesResponseType(typeof(ApiResponse<ShipmentCompletedResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<IResult> CompleteShipment([FromRoute] string shipmentId)
    {
        if (string.IsNullOrWhiteSpace(shipmentId))
            throw new GatewayInputException("Shipment ID is required.", "ShipmentId");

        return ForwardAsync(
            Service,
            $"/api/v1/shipment/{shipmentId}/complete",
            HttpMethod.Post,
            mockSeed: "shipment-complete"
        );
    }

    /// <summary>Handles carrier shipment status webhook.</summary>
    [HttpPost("shipments/webhook")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<IResult> ShipmentWebhook([FromBody] ShipmentWebhookRequest body)
    {
        if (string.IsNullOrWhiteSpace(body.TrackingNumber))
            throw new GatewayInputException("Tracking number is required.", "TrackingNumber");
        FulfillmentValidation.EnsureShipmentStatus(body.Status);

        return ForwardAsync(
            Service,
            "/api/v1/shipments/webhook",
            HttpMethod.Post,
            body: body,
            mockSeed: "shipment-webhook"
        );
    }
}
