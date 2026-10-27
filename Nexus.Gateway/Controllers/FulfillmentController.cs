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
using Nexus.Gateway.Security;

/// <summary>
/// Fulfillment HTTP API — gateway-side slice of the F4 shipping flows (FUL-17 .. FUL-23).
/// </summary>
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

    /// <summary>Xem chi tiết một vận đơn kèm lịch sử hành trình (FUL-19)</summary>
    [HttpGet("shipments/{shipmentId}")]
    [Authorize(Policy = GatewayPolicies.Customer)]
    public Task<IResult> GetShipment(string shipmentId)
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

    /// <summary>Liệt kê các vận đơn của một đơn hàng (FUL-18)</summary>
    [HttpGet("shipments/order/{orderId}")]
    [Authorize(Policy = GatewayPolicies.Customer)]
    public Task<IResult> GetShipmentsByOrder(string orderId)
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

    /// <summary>Danh sách vận đơn của người bán, lọc theo trạng thái (FUL-20)</summary>
    [HttpGet("seller/shipment")]
    [Authorize(Policy = GatewayPolicies.Seller)]
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

    /// <summary>Tìm kiếm vận đơn của người bán theo từ khoá và khoảng ngày (FUL-20)</summary>
    [HttpGet("seller/shipment/search")]
    [Authorize(Policy = GatewayPolicies.Seller)]
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

    /// <summary>Người bán xem chi tiết vận đơn của mình (FUL-19, FUL-20)</summary>
    [HttpGet("seller/shipment/{shipmentId}")]
    [Authorize(Policy = GatewayPolicies.Seller)]
    public Task<IResult> GetSellerShipment(string shipmentId)
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

    /// <summary>Cập nhật trạng thái vận đơn nhận từ đơn vị vận chuyển (FUL-17)</summary>
    [HttpPatch("shipments/{shipmentId}/tracking")]
    [Authorize(Policy = GatewayPolicies.Authenticated)]
    public Task<IResult> UpdateShipmentTracking(string shipmentId, [FromBody] UpdateShipmentTrackingRequest body)
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

    /// <summary>Người mua xác nhận đã nhận hàng (FUL-23)</summary>
    [HttpPost("shipment/{shipmentId}/complete")]
    [Authorize(Policy = GatewayPolicies.Customer)]
    public Task<IResult> CompleteShipment(string shipmentId)
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

    /// <summary>Nhận webhook trạng thái từ đơn vị vận chuyển (FUL-17)</summary>
    [HttpPost("shipments/webhook")]
    [AllowAnonymous]
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
