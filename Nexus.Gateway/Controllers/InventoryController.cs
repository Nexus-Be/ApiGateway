namespace Nexus.Gateway.Controllers;

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
/// <remarks>
/// <para>Hai đường nhập kho theo hợp đồng F2: nhập theo lô <c>POST /api/v1/inventory/stock-in</c> và nhập cho một biến thể <c>POST /api/v1/variant/{variantId}/stock-in</c>.</para>
/// <para>PR trước đó tự đặt route <c>/catalog/inventory/{productId}(/adjust)</c> — không có trong hợp đồng và đã bị loại bỏ.</para>
/// <para>Quyền: <c>seller</c> hoặc <c>admin</c>; response bọc <c>ApiResponse</c>.</para>
/// </remarks>
[ApiController]
[Route("api/v1")]
[BackendTag("catalog")]
[EnableRateLimiting("api")]
public sealed class InventoryController : ControllerBase
{
    private const string Service = "catalog";

    private readonly IGatewayDispatcher _dispatcher;
    private readonly CatalogApi.CatalogApiClient _client;

    public InventoryController(IGatewayDispatcher dispatcher, CatalogApi.CatalogApiClient client)
    {
        _dispatcher = dispatcher;
        _client = client;
    }

    /// <summary>Nhập kho theo lô nhiều SKU (CAT-09)</summary>
    /// <remarks>
    /// <para>Quyền: Seller hoặc Admin.</para>
    /// <para>Body: <c>items[]</c> (skuId, quantity &gt; 0). Tối đa 100 dòng mỗi lần, không nhận skuId trùng trong cùng một request.</para>
    /// <para>Response: <c>data.items[]</c> (skuId, addedQuantity, stockQuantity).</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: items rỗng, quá 100 dòng, quantity &lt;= 0, skuId sai UUID hoặc bị lặp</response>
    /// <response code="401">UNAUTHORIZED</response>
    /// <response code="403">FORBIDDEN: không phải Seller hoặc Admin</response>
    /// <response code="404">SKU_NOT_FOUND (Real)</response>
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

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "inventory-stock-in",
            o => _client.StockInAsync(request, o),
            StockInResponse.Parser,
            successMessage: "Stock updated successfully.");
    }

    /// <summary>Nhập kho cho một biến thể (CAT-10)</summary>
    /// <remarks>
    /// <para>Quyền: chủ sản phẩm hoặc Admin.</para>
    /// <para>Body: <c>quantity</c> &gt; 0. Response: skuId, addedQuantity, stockQuantity (một object, không có <c>items</c>).</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: quantity &lt;= 0, variantId sai UUID</response>
    /// <response code="401">UNAUTHORIZED</response>
    /// <response code="403">FORBIDDEN (Real)</response>
    /// <response code="404">VARIANT_NOT_FOUND (Real)</response>
    [HttpPost("variant/{variantId}/stock-in")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<StockInVariantResponse>), StatusCodes.Status200OK)]
    public Task<IResult> StockInVariant([FromRoute] string variantId, [FromBody] StockInVariantDto body)
    {
        var quantity = body.Quantity;
        CatalogValidation.EnsureStockInVariant(variantId, quantity);

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "variant-stock-in",
            o => _client.StockInVariantAsync(new StockInVariantRequest
            {
                VariantId = variantId,
                Quantity = quantity,
                Caller = CatalogValidation.Caller(HttpContext)
            }, o),
            StockInVariantResponse.Parser,
            successMessage: "Stock updated successfully.");
    }
}
