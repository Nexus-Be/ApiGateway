namespace Nexus.Gateway.Tests;

using System;
using System.IO;
using System.Text.Json;
using Xunit;

/// <summary>
/// Conformance gate for the catalog seed fixtures: every <c>Seeds/catalog/{operation}.json</c> must
/// exist and be valid JSON with a valid object or array root structure.
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
    public void Seed_is_valid_json_structure(string operation)
    {
        var path = Path.Combine(SeedsDirectory(), $"{operation}.json");
        Assert.True(File.Exists(path), $"Missing seed fixture: {path}");

        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.ValueKind == JsonValueKind.Object || doc.RootElement.ValueKind == JsonValueKind.Array);
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
