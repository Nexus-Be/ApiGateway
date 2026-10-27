namespace Nexus.Gateway.Tests;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

/// <summary>
/// Request-level coverage for the catalog mock surface (F2). Every route is asserted against the
/// contract path, its HTTP status and the response envelope documented in specs/02.
/// </summary>
public sealed class CatalogMockTests
{
    private const string ProductId = "22222222-2222-4222-8222-000000000001";
    private const string VariantId = "44444444-4444-4444-8444-000000000001";
    private const string CategoryId = "33333333-3333-4333-8333-000000000003";
    private const string SkuId = "44444444-4444-4444-8444-000000000001";
    private const string AuctionId = "55555555-5555-4555-8555-000000000001";

    private static JsonDocument Parse(string content) => JsonDocument.Parse(content);

    private static async Task<JsonDocument> OkJson(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Parse(await response.Content.ReadAsStringAsync());
    }

    private static void AssertSuccessEnvelope(JsonDocument doc, int statusCode)
    {
        var root = doc.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(statusCode, root.GetProperty("statusCode").GetInt32());
    }

    // ------------------------------------------------------------------ products (read)

    [Fact]
    public async Task ListProducts_ReturnsPagedEnvelope()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var response = await client.GetAsync("/api/v1/products?page=1&pageSize=10");
        using var doc = await OkJson(response);

        AssertSuccessEnvelope(doc, 200);
        var data = doc.RootElement.GetProperty("data");
        Assert.True(data.GetProperty("items").GetArrayLength() > 0);
        Assert.Equal(1, data.GetProperty("pagination").GetProperty("pageIndex").GetInt32());
        Assert.Equal(10, data.GetProperty("pagination").GetProperty("pageSize").GetInt32());
        Assert.True(data.GetProperty("pagination").GetProperty("totalCount").GetInt32() > 0);
    }

    [Fact]
    public async Task SearchProducts_ProductsSearchRoute_ReturnsPagedEnvelope()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var response = await client.GetAsync("/api/v1/products/search?page=1&pageSize=10");
        using var doc = await OkJson(response);

        AssertSuccessEnvelope(doc, 200);
        var data = doc.RootElement.GetProperty("data");
        Assert.True(data.GetProperty("items").GetArrayLength() > 0);
    }

    [Fact]
    public async Task ListProducts_RejectsInvalidPageSize()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var response = await client.GetAsync("/api/v1/products?page=1&pageSize=1000");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var doc = Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("VAL_INVALID_INPUT", doc.RootElement.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task SearchProducts_ReturnsPagedEnvelope()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor("seller-1", "seller"));

        var response = await client.GetAsync("/api/v1/product/search?keyword=headphones&minRating=4&page=1&pageSize=20");
        using var doc = await OkJson(response);

        AssertSuccessEnvelope(doc, 200);
        Assert.True(doc.RootElement.GetProperty("data").GetProperty("items").GetArrayLength() > 0);
    }

    [Fact]
    public async Task SearchProducts_RejectsInvertedPriceRange()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor("seller-1", "seller"));

        var response = await client.GetAsync("/api/v1/product/search?minPrice=100&maxPrice=10");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetProduct_ReturnsDetailShape()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var response = await client.GetAsync($"/api/v1/product/{ProductId}");
        using var doc = await OkJson(response);

        AssertSuccessEnvelope(doc, 200);
        var data = doc.RootElement.GetProperty("data");
        Assert.Equal(ProductId, data.GetProperty("product").GetProperty("productId").GetString());
        Assert.True(data.GetProperty("variants").GetArrayLength() > 0);
        Assert.True(data.GetProperty("images").GetArrayLength() > 0);
        Assert.True(data.GetProperty("rating").TryGetProperty("reviewCount", out _));
    }

    [Fact]
    public async Task GetProduct_RejectsMalformedIdentifier()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var response = await client.GetAsync("/api/v1/product/not-a-uuid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ------------------------------------------------------------------ products (write)

    [Fact]
    public async Task CreateProduct_Returns201WithDraftStatus()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor("seller-1", "seller"));

        var response = await client.PostAsJsonAsync("/api/v1/products", new
        {
            categoryId = CategoryId,
            name = "Nexus Mechanical Keyboard",
            description = "Hot-swap 75% keyboard.",
            brand = "NexusInput",
            specifications = new { layout = "75%" },
            variants = new[]
            {
                new
                {
                    skuCode = "NX-KB-001",
                    name = "Graphite",
                    price = 1890000,
                    currency = "VND",
                    isActive = true,
                    attributes = new[] { new { attrName = "Color", attrValue = "Graphite" } }
                }
            }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var doc = Parse(await response.Content.ReadAsStringAsync());
        AssertSuccessEnvelope(doc, 201);
        Assert.Equal("DRAFT", doc.RootElement.GetProperty("data").GetProperty("status").GetString());
        Assert.Equal("REGULAR", doc.RootElement.GetProperty("data").GetProperty("saleType").GetString());
    }

    [Fact]
    public async Task CreateProduct_RequiresName()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor("seller-1", "seller"));

        var response = await client.PostAsJsonAsync("/api/v1/products", new
        {
            categoryId = CategoryId,
            name = "",
            variants = new[]
            {
                new { skuCode = "NX-KB-001", name = "Graphite", price = 1890000, isActive = true }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var doc = Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("VAL_INVALID_INPUT", doc.RootElement.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task CreateProduct_RejectsNonPositivePrice()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor("seller-1", "seller"));

        var response = await client.PostAsJsonAsync("/api/v1/products", new
        {
            categoryId = CategoryId,
            name = "Nexus Mechanical Keyboard",
            variants = new[]
            {
                new { skuCode = "NX-KB-001", name = "Graphite", price = 0, isActive = true }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProduct_ReturnsUpdatedShape()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor("seller-1", "seller"));

        var response = await client.PatchAsJsonAsync($"/api/v1/product/{ProductId}", new
        {
            name = "Nexus Wireless Headphones Pro",
            brand = "NexusAudio"
        });
        using var doc = await OkJson(response);

        AssertSuccessEnvelope(doc, 200);
        Assert.Equal(ProductId, doc.RootElement.GetProperty("data").GetProperty("productId").GetString());
        Assert.True(doc.RootElement.GetProperty("data").TryGetProperty("updatedAt", out _));
    }

    [Fact]
    public async Task UpdateProduct_RejectsEmptyBody()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor("seller-1", "seller"));

        var response = await client.PatchAsJsonAsync($"/api/v1/product/{ProductId}", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteProduct_Returns204WithoutBody()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor("seller-1", "seller"));

        var response = await client.DeleteAsync($"/api/v1/product/{ProductId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
    }

    // ------------------------------------------------------------------ images

    [Fact]
    public async Task AddProductImages_Returns201WithFourFieldItems()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor("seller-1", "seller"));

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("not-a-real-jpeg-but-the-gateway-never-reads-it"));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "files", "headphones-3.jpg");
        form.Add(new StringContent("Nexus Wireless Headphones"), "altText");
        form.Add(new StringContent("true"), "isActive");

        var response = await client.PostAsync($"/api/v1/product/{ProductId}pics", form);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var doc = Parse(await response.Content.ReadAsStringAsync());
        AssertSuccessEnvelope(doc, 201);
        var item = doc.RootElement.GetProperty("data").GetProperty("items")[0];
        Assert.True(item.TryGetProperty("imageId", out _));
        Assert.True(item.TryGetProperty("sortOrder", out _));
        Assert.False(item.TryGetProperty("altText", out _));
    }

    [Fact]
    public async Task AddProductImages_RequiresAtLeastOneFile()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor("seller-1", "seller"));

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Nexus Wireless Headphones"), "altText");

        var response = await client.PostAsync($"/api/v1/product/{ProductId}pics", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListProductImages_ReturnsSixFieldItems()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var response = await client.GetAsync($"/api/v1/product/{ProductId}pics");
        using var doc = await OkJson(response);

        AssertSuccessEnvelope(doc, 200);
        var item = doc.RootElement.GetProperty("data").GetProperty("items")[0];
        Assert.True(item.TryGetProperty("altText", out _));
        Assert.True(item.TryGetProperty("variantId", out _));
    }

    [Fact]
    public async Task DeleteProductImages_Returns204()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor("seller-1", "seller"));

        var response = await client.DeleteAsync(
            $"/api/v1/product/{ProductId}pics?ids=66666666-6666-4666-8666-000000000001");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteProductImages_RequiresIds()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor("seller-1", "seller"));

        var response = await client.DeleteAsync($"/api/v1/product/{ProductId}pics");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ------------------------------------------------------------------ rating

    [Fact]
    public async Task GetProductTotalRating_ReturnsSummary()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var response = await client.GetAsync($"/api/v1/products/{ProductId}/totalRating");
        using var doc = await OkJson(response);

        AssertSuccessEnvelope(doc, 200);
        var data = doc.RootElement.GetProperty("data");
        Assert.Equal(ProductId, data.GetProperty("productId").GetString());
        Assert.True(data.GetProperty("reviewCount").GetInt32() > 0);
    }

    [Fact]
    public async Task GetProductRating_KeepsRatingSiblingAndPaging()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var response = await client.GetAsync($"/api/v1/products/{ProductId}/rating?page=1&pageSize=20&rating=5");
        using var doc = await OkJson(response);

        AssertSuccessEnvelope(doc, 200);
        var data = doc.RootElement.GetProperty("data");
        Assert.True(data.TryGetProperty("rating", out _));
        Assert.True(data.GetProperty("items").GetArrayLength() > 0);
        Assert.True(data.TryGetProperty("page", out _));
        Assert.True(data.TryGetProperty("total", out _));
    }

    [Fact]
    public async Task GetProductRating_RejectsOutOfRangeRating()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var response = await client.GetAsync($"/api/v1/products/{ProductId}/rating?rating=9");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ------------------------------------------------------------------ status

    [Fact]
    public async Task UpdateProductStatus_ReturnsStatusEnvelope()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor("seller-1", "seller"));

        var response = await client.PatchAsJsonAsync($"/api/v1/product/{ProductId}/status", new
        {
            status = "ACTIVE"
        });
        using var doc = await OkJson(response);

        AssertSuccessEnvelope(doc, 200);
        Assert.Equal("ACTIVE", doc.RootElement.GetProperty("data").GetProperty("status").GetString());
    }

    [Fact]
    public async Task UpdateProductStatus_RequiresReasonWhenInactive()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor("seller-1", "seller"));

        var response = await client.PatchAsJsonAsync($"/api/v1/product/{ProductId}/status", new
        {
            status = "INACTIVE"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProductStatus_RejectsUnknownStatus()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor("seller-1", "seller"));

        var response = await client.PatchAsJsonAsync($"/api/v1/product/{ProductId}/status", new
        {
            status = "ARCHIVED"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ------------------------------------------------------------------ variants

    [Fact]
    public async Task CreateVariant_Returns201()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor("seller-1", "seller"));

        var response = await client.PostAsJsonAsync($"/api/v1/product/{ProductId}/variants", new
        {
            skuCode = "NX-HP-001-BLU",
            name = "Deep Blue",
            price = 1290000,
            currency = "VND",
            attributes = new[] { new { attrName = "Color", attrValue = "Blue" } }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var doc = Parse(await response.Content.ReadAsStringAsync());
        AssertSuccessEnvelope(doc, 201);
        Assert.Equal("NX-HP-001-BLU", doc.RootElement.GetProperty("data").GetProperty("skuCode").GetString());
    }

    [Fact]
    public async Task CreateVariant_RejectsNonPositivePrice()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor("seller-1", "seller"));

        var response = await client.PostAsJsonAsync($"/api/v1/product/{ProductId}/variants", new
        {
            skuCode = "NX-HP-001-BLU",
            name = "Deep Blue",
            price = -1
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetVariant_ReturnsDetailShape()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor("seller-1", "seller"));

        var response = await client.GetAsync($"/api/v1/variant/{VariantId}");
        using var doc = await OkJson(response);

        AssertSuccessEnvelope(doc, 200);
        var data = doc.RootElement.GetProperty("data");
        Assert.Equal(VariantId, data.GetProperty("variantId").GetString());
        Assert.True(data.TryGetProperty("isDefault", out _));
        Assert.True(data.TryGetProperty("hasOrder", out _));
        Assert.True(data.GetProperty("images").GetArrayLength() > 0);
    }

    [Fact]
    public async Task UpdateVariant_ReturnsUpdatedShape()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor("seller-1", "seller"));

        var response = await client.PatchAsJsonAsync($"/api/v1/variant/{VariantId}", new
        {
            name = "Matte Black",
            price = 1350000
        });
        using var doc = await OkJson(response);

        AssertSuccessEnvelope(doc, 200);
        Assert.Equal(VariantId, doc.RootElement.GetProperty("data").GetProperty("variantId").GetString());
    }

    [Fact]
    public async Task DeleteVariant_Returns204()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor("seller-1", "seller"));

        var response = await client.DeleteAsync($"/api/v1/variant/{VariantId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
    }

    // ------------------------------------------------------------------ categories

    [Fact]
    public async Task ListCategories_ReturnsTreeItems()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var response = await client.GetAsync("/api/v1/categories?tree=true");
        using var doc = await OkJson(response);

        AssertSuccessEnvelope(doc, 200);
        var item = doc.RootElement.GetProperty("data").GetProperty("items")[0];
        Assert.True(item.GetProperty("children").GetArrayLength() > 0);
    }

    [Fact]
    public async Task GetCategory_ReturnsParentAndChildren()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var response = await client.GetAsync($"/api/v1/categories/{CategoryId}");
        using var doc = await OkJson(response);

        AssertSuccessEnvelope(doc, 200);
        var data = doc.RootElement.GetProperty("data");
        Assert.Equal(CategoryId, data.GetProperty("category").GetProperty("categoryId").GetString());
        Assert.True(data.TryGetProperty("parent", out _));
        Assert.True(data.GetProperty("children").GetArrayLength() > 0);
    }

    [Fact]
    public async Task CreateCategory_Returns201()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor("admin-1", "admin"));

        var response = await client.PostAsJsonAsync("/api/v1/categories", new
        {
            name = "Laptops",
            description = "Portable computers.",
            parentId = "33333333-3333-4333-8333-000000000001",
            sortOrder = 3
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var doc = Parse(await response.Content.ReadAsStringAsync());
        AssertSuccessEnvelope(doc, 201);
        Assert.True(doc.RootElement.GetProperty("data").GetProperty("level").GetInt32() > 0);
    }

    [Fact]
    public async Task UpdateCategory_ReturnsUpdatedShape()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor("admin-1", "admin"));

        var response = await client.PutAsJsonAsync($"/api/v1/categories/{CategoryId}", new
        {
            name = "Audio and Headphones",
            description = "Speakers, headphones and accessories.",
            parentId = "33333333-3333-4333-8333-000000000001",
            sortOrder = 2,
            isActive = true
        });
        using var doc = await OkJson(response);

        AssertSuccessEnvelope(doc, 200);
        Assert.True(doc.RootElement.GetProperty("data").TryGetProperty("updatedAt", out _));
    }

    [Fact]
    public async Task DeleteCategory_Returns204()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor("admin-1", "admin"));

        var response = await client.DeleteAsync($"/api/v1/categories/{CategoryId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
    }

    // ------------------------------------------------------------------ inventory

    [Fact]
    public async Task StockIn_ReturnsPerSkuResults()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor("seller-1", "seller"));

        var response = await client.PostAsJsonAsync("/api/v1/inventory/stock-in", new
        {
            items = new[] { new { skuId = SkuId, quantity = 50 } }
        });
        using var doc = await OkJson(response);

        AssertSuccessEnvelope(doc, 200);
        var item = doc.RootElement.GetProperty("data").GetProperty("items")[0];
        Assert.Equal(SkuId, item.GetProperty("skuId").GetString());
        Assert.True(item.GetProperty("stockQuantity").GetInt32() >= item.GetProperty("addedQuantity").GetInt32());
    }

    [Fact]
    public async Task StockIn_RejectsNonPositiveQuantity()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor("seller-1", "seller"));

        var response = await client.PostAsJsonAsync("/api/v1/inventory/stock-in", new
        {
            items = new[] { new { skuId = SkuId, quantity = 0 } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task StockInVariant_ReturnsSingleObject()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor("seller-1", "seller"));

        var response = await client.PostAsJsonAsync($"/api/v1/variant/{VariantId}/stock-in", new
        {
            quantity = 25
        });
        using var doc = await OkJson(response);

        AssertSuccessEnvelope(doc, 200);
        var data = doc.RootElement.GetProperty("data");
        Assert.True(data.TryGetProperty("skuId", out _));
        Assert.False(data.TryGetProperty("items", out _));
    }

    // ------------------------------------------------------------------ auction products

    [Fact]
    public async Task CreateAuctionProduct_Returns201WithAuctionSaleType()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor("seller-1", "seller"));

        var response = await client.PostAsJsonAsync("/api/v1/product/auction", new
        {
            categoryId = CategoryId,
            name = "Nexus Limited Edition Turntable",
            description = "Serial-numbered belt-drive turntable, 1 of 50.",
            brand = "NexusAudio",
            variant = new
            {
                skuCode = "NX-TT-002",
                name = "Single edition",
                attributes = new[] { new { attrName = "Edition", attrValue = "1 of 50" } }
            }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var doc = Parse(await response.Content.ReadAsStringAsync());
        AssertSuccessEnvelope(doc, 201);
        var data = doc.RootElement.GetProperty("data");
        Assert.Equal("AUCTION", data.GetProperty("saleType").GetString());
        Assert.True(data.TryGetProperty("variant", out _));
    }

    [Fact]
    public async Task SearchAuctionProducts_ReturnsPagedEnvelope()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor("seller-1", "seller"));

        var response = await client.GetAsync("/api/v1/product/auction/search?keyword=turntable");
        using var doc = await OkJson(response);

        AssertSuccessEnvelope(doc, 200);
        var item = doc.RootElement.GetProperty("data").GetProperty("items")[0];
        Assert.Equal("AUCTION", item.GetProperty("saleType").GetString());
    }

    [Fact]
    public async Task ListAuctionProducts_ReturnsPagedEnvelope()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor("seller-1", "seller"));

        var response = await client.GetAsync("/api/v1/product/auctions?status=RUNNING");
        using var doc = await OkJson(response);

        AssertSuccessEnvelope(doc, 200);
        Assert.True(doc.RootElement.GetProperty("data").GetProperty("items").GetArrayLength() > 0);
    }

    [Fact]
    public async Task GetAuctionProduct_ReturnsAuctionAndProduct()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor("seller-1", "seller"));

        var response = await client.GetAsync($"/api/v1/product/auction/{AuctionId}");
        using var doc = await OkJson(response);

        AssertSuccessEnvelope(doc, 200);
        var data = doc.RootElement.GetProperty("data");
        Assert.Equal(AuctionId, data.GetProperty("auction").GetProperty("auctionId").GetString());
        Assert.True(data.GetProperty("product").TryGetProperty("variants", out _));
    }

    // ------------------------------------------------------------------ contract-level guards

    [Fact]
    public async Task LegacyCatalogSegment_IsGone()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var response = await client.GetAsync("/api/v1/catalog/products");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/product/not-a-uuid")]
    [InlineData("/api/v1/variant/not-a-uuid")]
    [InlineData("/api/v1/categories/not-a-uuid")]
    [InlineData("/api/v1/product/auction/not-a-uuid")]
    public async Task MalformedIdentifiers_Return400(string path)
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor("seller-1", "seller"));

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
