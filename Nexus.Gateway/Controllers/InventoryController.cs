namespace Nexus.Gateway.Controllers;

using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.Gateway.Common;
using Nexus.Gateway.Dispatching;
using Nexus.Gateway.Dtos.Catalog;
using Nexus.Gateway.Responses;
using Nexus.Gateway.Security;
using ShoppingCart.Contracts.V1;

/// <summary>
/// Inventory HTTP API — gateway-side slice of the F2 stock flows (CAT-09, CAT-10).
/// </summary>
[ApiController]
[Route("api/v1")]
[BackendTag("catalog")]
[EnableRateLimiting("api")]
public sealed class InventoryController : GatewayBaseController
{
    private const string Service = "catalog";
    private readonly CatalogApi.CatalogApiClient _client;

    public InventoryController(IGatewayDispatcher dispatcher, CatalogApi.CatalogApiClient client)
        : base(dispatcher)
    {
        _client = client;
    }

    /// <summary>Nhập kho theo lô nhiều SKU (CAT-09)</summary>
    [HttpPost("inventory/stock-in")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<StockInResponse>), StatusCodes.Status200OK)]
    public Task<IResult> StockIn([FromBody] StockInDto body)
    {
        CatalogValidation.EnsureStockIn(body);

        var request = new StockInRequest { Caller = CatalogValidation.Caller(HttpContext) };
        foreach (var item in body.Items!)
        {
            request.Items.Add(new StockInItem
            {
                SkuId = item.SkuId!.Value.ToString(),
                Quantity = item.Quantity
            });
        }

        return ForwardAsync(
            Service,
            "/api/v1/inventory/stock-in",
            HttpMethod.Post,
            grpcCall: o => _client.StockInAsync(request, o),
            parser: StockInResponse.Parser,
            body: body,
            mockSeed: "inventory-stock-in",
            successMessage: "Stock updated successfully."
        );
    }

    /// <summary>Nhập kho cho một biến thể (CAT-10)</summary>
    [HttpPost("variant/{variantId}/stock-in")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<StockInVariantResponse>), StatusCodes.Status200OK)]
    public Task<IResult> StockInVariant([FromRoute] string variantId, [FromBody] StockInVariantDto body)
    {
        var quantity = body.Quantity;
        CatalogValidation.EnsureStockInVariant(variantId, quantity);

        return ForwardAsync(
            Service,
            $"/api/v1/variant/{variantId}/stock-in",
            HttpMethod.Post,
            grpcCall: o => _client.StockInVariantAsync(new StockInVariantRequest
            {
                VariantId = variantId,
                Quantity = quantity,
                Caller = CatalogValidation.Caller(HttpContext)
            }, o),
            parser: StockInVariantResponse.Parser,
            body: body,
            mockSeed: "variant-stock-in",
            successMessage: "Stock updated successfully."
        );
    }
}
