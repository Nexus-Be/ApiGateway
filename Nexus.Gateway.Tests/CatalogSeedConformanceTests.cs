namespace Nexus.Gateway.Tests;

using System.Text.Json;
using Google.Protobuf;
using ShoppingCart.Contracts.V1;
using Xunit;

/// <summary>
/// Conformance gate for the catalog seed fixtures: every <c>Seeds/catalog/{operation}.json</c> must
/// deserialize into the exact contract message the controller returns for that operation. A seed that
/// drifts from <c>catalog.proto</c> fails here instead of leaking through the gateway's silent
/// raw-JSON fallback in <c>MockDataMapper</c>.
/// </summary>
public sealed class CatalogSeedConformanceTests
{
    [Theory]
    [InlineData("products")]
    [InlineData("product-search")]
    [InlineData("product")]
    [InlineData("product-create")]
    [InlineData("product-update")]
    [InlineData("product-delete")]
    [InlineData("product-images-add")]
    [InlineData("product-images")]
    [InlineData("product-images-delete")]
    [InlineData("product-rating-total")]
    [InlineData("product-rating")]
    [InlineData("product-status")]
    [InlineData("variant-create")]
    [InlineData("variant-delete")]
    [InlineData("variant-update")]
    [InlineData("variant")]
    [InlineData("auction-product-create")]
    [InlineData("auction-product-search")]
    [InlineData("auction-products")]
    [InlineData("auction-product")]
    [InlineData("categories")]
    [InlineData("category")]
    [InlineData("category-create")]
    [InlineData("category-update")]
    [InlineData("category-delete")]
    [InlineData("inventory-stock-in")]
    [InlineData("variant-stock-in")]
    public void Seed_matches_its_contract_message(string operation)
    {
        var path = Path.Combine(SeedsDirectory(), $"{operation}.json");
        Assert.True(File.Exists(path), $"Missing seed fixture: {path}");

        var json = File.ReadAllText(path);
        var message = Parse(operation, json);

        Assert.False(string.IsNullOrEmpty(message.GetType().Name));

        // Round-trip: the canonical form must still be valid JSON for the same message.
        var canonical = JsonFormatter.Default.Format(message);
        _ = Parse(operation, canonical);
    }

    private static IMessage Parse(string operation, string json)
    {
        switch (operation)
        {
            case "products":
            case "product-search":
            case "auction-products":
            case "auction-product-search":
                return ProductListResponse.Parser.ParseJson(json);
            case "product":
                return ProductDetailResponse.Parser.ParseJson(json);
            case "product-create":
                return ProductCreatedResponse.Parser.ParseJson(json);
            case "product-update":
                return ProductUpdatedResponse.Parser.ParseJson(json);
            case "product-delete":
            case "product-images-delete":
            case "variant-delete":
            case "category-delete":
                return DeleteResponse.Parser.ParseJson(json);
            case "product-images-add":
                return ProductImageAddResponse.Parser.ParseJson(json);
            case "product-images":
                return ProductImageListResponse.Parser.ParseJson(json);
            case "product-rating-total":
                return ProductRatingSummaryResponse.Parser.ParseJson(json);
            case "product-rating":
                return ProductRatingResponse.Parser.ParseJson(json);
            case "product-status":
                return ProductStatusResponse.Parser.ParseJson(json);
            case "variant-create":
                return VariantCreatedResponse.Parser.ParseJson(json);
            case "variant-update":
                return VariantUpdatedResponse.Parser.ParseJson(json);
            case "variant":
                return VariantDetailResponse.Parser.ParseJson(json);
            case "auction-product-create":
                return AuctionProductCreatedResponse.Parser.ParseJson(json);
            case "auction-product":
                return AuctionProductDetailResponse.Parser.ParseJson(json);
            case "categories":
                return CategoryListResponse.Parser.ParseJson(json);
            case "category":
                return CategoryDetailResponse.Parser.ParseJson(json);
            case "category-create":
                return CategoryCreatedResponse.Parser.ParseJson(json);
            case "category-update":
                return CategoryUpdatedResponse.Parser.ParseJson(json);
            case "inventory-stock-in":
                return StockInResponse.Parser.ParseJson(json);
            case "variant-stock-in":
                return StockInVariantResponse.Parser.ParseJson(json);
            default:
                throw new Xunit.Sdk.XunitException($"No contract message mapped for seed '{operation}'.");
        }
    }

    private static string SeedsDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Nexus.Gateway", "Seeds", "catalog");
            if (Directory.Exists(candidate)) return candidate;

            candidate = Path.Combine(directory.FullName, "Seeds", "catalog");
            if (Directory.Exists(candidate)) return candidate;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Nexus.Gateway/Seeds/catalog from the test output directory.");
    }
}
