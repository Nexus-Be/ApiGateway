namespace Nexus.Gateway.Controllers;

using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.Gateway.Common;
using Nexus.Gateway.Dispatching;
using Nexus.Gateway.Dtos.Fulfillment;
using Nexus.Gateway.Security;

/// <summary>
/// F1 shipment surface (FUL-17 .. FUL-23). Routes follow the F1 contract sheet:
/// <c>/api/v1/shipments/*</c> and <c>/api/v1/seller/shipment*</c>.
/// </summary>
/// <remarks>
/// <para>Mock đọc <c>Seeds/fulfillment/*.json</c>; response bọc trong <c>ApiResponse&lt;T&gt;</c>.</para>
/// <para>Real chưa nối: contract gRPC của Fulfillment chưa tồn tại và <c>shop.proto</c> không được sửa,
/// nên <c>realCall</c> chỉ là placeholder không bao giờ được gọi trong Mock mode.</para>
/// </remarks>
[ApiController]
[Route("api/v1")]
[BackendTag("fulfillment")]
[EnableRateLimiting("api")]
public sealed class FulfillmentController : ControllerBase
{
    private const string Service = "fulfillment";

    private static readonly MessageParser<Struct> SeedParser = Struct.Parser;

    private readonly IGatewayDispatcher _dispatcher;

    public FulfillmentController(IGatewayDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    private static AsyncUnaryCall<Struct> PendingRealCall(CallOptions _) =>
        throw new NotSupportedException("The fulfillment gRPC contract is not wired yet.");

    /// <summary>Xem chi tiết một vận đơn kèm lịch sử hành trình (FUL-19)</summary>
    [HttpGet("shipments/{shipmentId}")]
    [Authorize(Policy = GatewayPolicies.Customer)]
    public Task<IResult> GetShipment(string shipmentId)
    {
        if (string.IsNullOrWhiteSpace(shipmentId))
            throw new GatewayInputException("Shipment ID is required.", "ShipmentId");

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "shipment-get", PendingRealCall, SeedParser);
    }

    /// <summary>Liệt kê các vận đơn của một đơn hàng (FUL-18)</summary>
    [HttpGet("shipments/order/{orderId}")]
    [Authorize(Policy = GatewayPolicies.Customer)]
    public Task<IResult> GetShipmentsByOrder(string orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            throw new GatewayInputException("Order ID is required.", "OrderId");

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "shipments-by-order", PendingRealCall, SeedParser);
    }

    /// <summary>Danh sách vận đơn của người bán, lọc theo trạng thái (FUL-20)</summary>
    [HttpGet("seller/shipment")]
    [Authorize(Policy = GatewayPolicies.Seller)]
    public Task<IResult> ListSellerShipments([FromQuery] PaginationRequest pagination, [FromQuery] string? status)
    {
        pagination.EnsureValid();

        return _dispatcher.DispatchPagedAsync(
            HttpContext, Service, "seller-shipments",
            pagination.Page, pagination.PageSize,
            PendingRealCall, SeedParser);
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

        return _dispatcher.DispatchPagedAsync(
            HttpContext, Service, "seller-shipments-search",
            pagination.Page, pagination.PageSize,
            PendingRealCall, SeedParser);
    }

    /// <summary>Người bán xem chi tiết vận đơn của mình (FUL-19, FUL-20)</summary>
    [HttpGet("seller/shipment/{shipmentId}")]
    [Authorize(Policy = GatewayPolicies.Seller)]
    public Task<IResult> GetSellerShipment(string shipmentId)
    {
        if (string.IsNullOrWhiteSpace(shipmentId))
            throw new GatewayInputException("Shipment ID is required.", "ShipmentId");

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "seller-shipment-get", PendingRealCall, SeedParser);
    }

    /// <summary>Cập nhật trạng thái vận đơn nhận từ đơn vị vận chuyển (FUL-17)</summary>
    [HttpPatch("shipments/{shipmentId}/tracking")]
    [Authorize(Policy = GatewayPolicies.Authenticated)]
    public Task<IResult> UpdateShipmentTracking(string shipmentId, [FromBody] UpdateShipmentTrackingRequest body)
    {
        if (string.IsNullOrWhiteSpace(shipmentId))
            throw new GatewayInputException("Shipment ID is required.", "ShipmentId");
        if (string.IsNullOrWhiteSpace(body.Status))
            throw new GatewayInputException("Shipment status is required.", "Status");
        if (string.IsNullOrWhiteSpace(body.Location))
            throw new GatewayInputException("Location is required.", "Location");

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "shipment-tracking-update", PendingRealCall, SeedParser);
    }

    /// <summary>Người mua xác nhận đã nhận hàng (FUL-23)</summary>
    [HttpPost("shipment/{shipmentId}/complete")]
    [Authorize(Policy = GatewayPolicies.Customer)]
    public Task<IResult> CompleteShipment(string shipmentId)
    {
        if (string.IsNullOrWhiteSpace(shipmentId))
            throw new GatewayInputException("Shipment ID is required.", "ShipmentId");

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "shipment-complete", PendingRealCall, SeedParser);
    }

    /// <summary>Nhận webhook trạng thái từ đơn vị vận chuyển (FUL-17)</summary>
    [HttpPost("shipments/webhook")]
    [AllowAnonymous]
    public Task<IResult> ShipmentWebhook([FromBody] ShipmentWebhookRequest body)
    {
        if (string.IsNullOrWhiteSpace(body.TrackingNumber))
            throw new GatewayInputException("Tracking number is required.", "TrackingNumber");
        if (string.IsNullOrWhiteSpace(body.Status))
            throw new GatewayInputException("Shipment status is required.", "Status");

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "shipment-webhook", PendingRealCall, SeedParser);
    }
}
