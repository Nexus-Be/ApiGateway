namespace Nexus.Gateway.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.Gateway.Common;
using Nexus.Gateway.Dispatching;
using Nexus.Gateway.Dtos.Fulfillment;
using Nexus.Gateway.Responses;
using Nexus.Gateway.Security;
using ShoppingCart.Contracts.V1;

[ApiController]
[Route("api/v1/fulfillment")]
[BackendTag("fulfillment")]
[EnableRateLimiting("api")]
public sealed class FulfillmentController : ControllerBase
{
    private readonly IGatewayDispatcher _dispatcher;
    private readonly FulfillmentApi.FulfillmentApiClient _client;

    public FulfillmentController(IGatewayDispatcher dispatcher, FulfillmentApi.FulfillmentApiClient client)
    {
        _dispatcher = dispatcher;
        _client = client;
    }

    [HttpGet("inventory/{skuId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<FulfillmentInventory>), StatusCodes.Status200OK)]
    public Task<IResult> GetInventory([FromRoute] string skuId)
    {
        if (string.IsNullOrWhiteSpace(skuId))
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "skuId là bắt buộc", field: "SkuId"));

        return _dispatcher.DispatchAsync(
            HttpContext,
            "fulfillment",
            "inventory",
            o => _client.GetInventoryAsync(new IdRequest { Id = skuId }, o),
            FulfillmentInventory.Parser,
            successMessage: "Inventory retrieved successfully."
        );
    }

    [HttpPost("shipping/quote")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<ShippingFeeQuote>), StatusCodes.Status200OK)]
    public Task<IResult> CalculateShippingFee([FromBody] ShippingQuoteRequestDto body)
    {
        if (string.IsNullOrWhiteSpace(body.SellerAddress))
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "sellerAddress là bắt buộc", field: "SellerAddress"));
        if (string.IsNullOrWhiteSpace(body.BuyerAddress))
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "buyerAddress là bắt buộc", field: "BuyerAddress"));
        if (body.WeightGrams <= 0)
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "weightGrams phải > 0", field: "WeightGrams"));

        return _dispatcher.DispatchAsync(
            HttpContext,
            "fulfillment",
            "shipping-quote",
            o => _client.CalculateShippingFeeAsync(new ShippingFeeRequest
            {
                SellerAddress = body.SellerAddress,
                BuyerAddress = body.BuyerAddress,
                WeightGrams = body.WeightGrams
            }, o),
            ShippingFeeQuote.Parser,
            successMessage: "Shipping quote calculated."
        );
    }

    [HttpGet("shipments/{trackingCode}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<ShipmentTracking>), StatusCodes.Status200OK)]
    public Task<IResult> GetShipmentTracking([FromRoute] string trackingCode)
    {
        if (string.IsNullOrWhiteSpace(trackingCode))
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "trackingCode là bắt buộc", field: "TrackingCode"));

        return _dispatcher.DispatchAsync(
            HttpContext,
            "fulfillment",
            "shipment-tracking",
            o => _client.GetShipmentTrackingAsync(new IdRequest { Id = trackingCode }, o),
            ShipmentTracking.Parser,
            successMessage: "Shipment tracking retrieved."
        );
    }
}
