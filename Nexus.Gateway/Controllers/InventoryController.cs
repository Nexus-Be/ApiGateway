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

[ApiController]
[Route("api/v1")]
[BackendTag("catalog")]
[EnableRateLimiting("api")]
public sealed class InventoryController : GatewayBaseController
{
    private const string Service = "catalog";

    public InventoryController(IGatewayDispatcher dispatcher)
        : base(dispatcher)
    {
    }

    /// <summary>Batch stocks in inventory for multiple SKUs.</summary>
    [HttpPost("inventory/stock-in")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<StockInResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> StockIn([FromBody] StockInDto body)
    {
        CatalogValidation.EnsureStockIn(body);

        return ForwardAsync(
            Service,
            "/api/v1/inventory/stock-in",
            HttpMethod.Post,
            body: body,
            mockSeed: "inventory-stock-in",
            successMessage: "Stock updated successfully."
        );
    }

    /// <summary>Stocks in inventory for a specific variant.</summary>
    [HttpPost("variant/{variantId}/stock-in")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<StockInVariantResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> StockInVariant([FromRoute] string variantId, [FromBody] StockInVariantDto body)
    {
        var quantity = body.Quantity;
        CatalogValidation.EnsureStockInVariant(variantId, quantity);

        return ForwardAsync(
            Service,
            $"/api/v1/variant/{variantId}/stock-in",
            HttpMethod.Post,
            body: body,
            mockSeed: "variant-stock-in",
            successMessage: "Stock updated successfully."
        );
    }
}
