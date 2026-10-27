using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Nexus.Gateway.Tests;

/// <summary>
/// Auction endpoints in Mock mode: every route is served by AuctionController and reads Seeds/auction/*.json.
/// </summary>
public sealed class AuctionMockTests
{
    private const string AuctionId = "a1000000-0000-0000-0000-000000000001";
    private const string UserId = "11111111-1111-1111-1111-000000000002";

    private static string Future(int days) => DateTimeOffset.UtcNow.AddDays(days).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

    private static string CreateBody() =>
        $$"""{"productId":"b1000000-0000-0000-0000-000000000001","skuId":"c1000000-0000-0000-0000-000000000001","startPriceAmount":1000000,"bidStepAmount":50000,"scheduledStartAt":"{{Future(1)}}","scheduledEndAt":"{{Future(3)}}"}""";

    private static string ScheduleBody() =>
        $$"""{"startPriceAmount":1000000,"scheduledStartAt":"{{Future(1)}}","scheduledEndAt":"{{Future(3)}}"}""";

    public static IEnumerable<object?[]> HappyPaths() =>
    [
        // method, url, role, body, expected status
        ["GET", "/api/v1/auctions", null, null, 200],
        ["GET", "/api/v1/auctions?page=1&pageSize=10&status=RUNNING", null, null, 200],
        ["GET", $"/api/v1/auctions/{AuctionId}", null, null, 200],
        ["GET", $"/api/v1/auctions/{AuctionId}/bids?page=1&pageSize=20", null, null, 200],
        ["GET", $"/api/v1/auctions/{AuctionId}/result", "buyer", null, 200],
        ["GET", "/api/v1/me/bids", "buyer", null, 200],
        ["POST", "/api/v1/auctions", "seller", CreateBody(), 201],
        ["PUT", $"/api/v1/auctions/{AuctionId}", "seller", """{"startPriceAmount":1200000}""", 200],
        ["POST", $"/api/v1/auctions/{AuctionId}/cancel", "seller", """{"reason":"Changed my mind"}""", 200],
        ["POST", $"/api/v1/auctions/{AuctionId}/cancel", "seller", null, 200],
        ["POST", $"/api/v1/auctions/{AuctionId}/recreate", "seller", ScheduleBody(), 201],
        ["GET", "/api/v1/seller/auctions", "seller", null, 200],
        ["GET", $"/api/v1/auctions/{AuctionId}/status-history", "staff", null, 200],
        ["GET", $"/api/v1/admin/auctions/{AuctionId}/saga", "staff", null, 200],
        ["POST", $"/api/v1/admin/auctions/{AuctionId}/force-cancel", "admin", """{"reason":"Suspicious activity"}""", 200],
        ["GET", "/api/v1/admin/auction-configs", "admin", null, 200],
        ["PUT", "/api/v1/admin/auction-configs/bid_step_percent", "admin", """{"configValue":10,"description":"Default bid step"}""", 200],
        ["GET", "/internal/v1/auctions/status?productId=22222222-2222-4222-8222-000000000001", null, null, 200],
        ["GET", $"/internal/v1/auctions/{AuctionId}/summary", null, null, 200],
    ];

    [Theory]
    [MemberData(nameof(HappyPaths))]
    public async Task Endpoint_serves_seed_data_in_mock_mode(string method, string url, string? role, string? body, int expected)
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        Authorize(client, role);

        var response = await client.SendAsync(Request(method, url, body));

        Assert.Equal((HttpStatusCode)expected, response.StatusCode);
        Assert.Equal("Mock", response.Headers.GetValues("X-Gateway-Mode").Single());
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task Place_bid_in_mock_mode_returns_created()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        Authorize(client, "buyer");

        var request = Request("POST", $"/api/v1/auctions/{AuctionId}/bids", """{"amount":13000000}""");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Headers.Add("If-Match", "\"5\"");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task List_returns_standard_pagination_envelope()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var response = await client.GetAsync("/api/v1/auctions?page=1&pageSize=20");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = doc.RootElement.GetProperty("data");

        Assert.True(data.GetProperty("items").GetArrayLength() > 0);
        Assert.Equal(1, data.GetProperty("pagination").GetProperty("pageIndex").GetInt32());
    }

    [Theory]
    [InlineData("GET", "/api/v1/auctions/not-a-uuid", null, null)]
    [InlineData("GET", "/api/v1/auctions?page=0", null, null)]
    [InlineData("GET", "/api/v1/auctions?status=BOGUS", null, null)]
    [InlineData("GET", "/api/v1/me/bids?pageSize=500", "buyer", null)]
    [InlineData("POST", "/api/v1/auctions", "seller", """{"productId":"x","skuId":"y"}""")]
    [InlineData("PUT", "/api/v1/auctions/a1000000-0000-0000-0000-000000000001", "seller", "{}")]
    [InlineData("POST", "/api/v1/auctions/a1000000-0000-0000-0000-000000000001/cancel", "seller", """{"reason":"   "}""")]
    [InlineData("POST", "/api/v1/admin/auctions/a1000000-0000-0000-0000-000000000001/force-cancel", "admin", """{"reason":""}""")]
    [InlineData("PUT", "/api/v1/admin/auction-configs/bid_step_percent", "admin", "{}")]
    [InlineData("POST", "/api/v1/auctions/a1000000-0000-0000-0000-000000000001/bids", "buyer", """{"amount":100}""")]
    public async Task Invalid_input_returns_validation_error(string method, string url, string? role, string? body)
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        Authorize(client, role);

        var response = await client.SendAsync(Request(method, url, body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("VAL_INVALID_INPUT", doc.RootElement.GetProperty("errorCode").GetString());
    }

    [Theory]
    [InlineData("POST", "/api/v1/auctions/a1000000-0000-0000-0000-000000000001/bids", 0)]
    [InlineData("POST", "/api/v1/auctions/a1000000-0000-0000-0000-000000000001/bids", -5)]
    public async Task Bid_amount_must_be_positive(string method, string url, int amount)
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        Authorize(client, "buyer");

        var request = Request(method, url, $$"""{"amount":{{amount}}}""");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Headers.Add("If-Match", "\"1\"");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/v1/seller/auctions", null, HttpStatusCode.Unauthorized)]
    [InlineData("GET", "/api/v1/seller/auctions", "buyer", HttpStatusCode.Forbidden)]
    [InlineData("GET", "/api/v1/me/bids", null, HttpStatusCode.Unauthorized)]
    [InlineData("GET", "/api/v1/admin/auction-configs", "seller", HttpStatusCode.Forbidden)]
    [InlineData("GET", "/api/v1/admin/auction-configs", "staff", HttpStatusCode.Forbidden)]
    [InlineData("GET", "/api/v1/admin/auctions/a1000000-0000-0000-0000-000000000001/saga", "buyer", HttpStatusCode.Forbidden)]
    public async Task Protected_endpoints_enforce_roles(string method, string url, string? role, HttpStatusCode expected)
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        Authorize(client, role);

        var response = await client.SendAsync(Request(method, url, null));

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Force_cancel_is_admin_only()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        Authorize(client, "staff");

        var response = await client.SendAsync(Request(
            "POST", $"/api/v1/admin/auctions/{AuctionId}/force-cancel", """{"reason":"Test"}"""));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static void Authorize(HttpClient client, string? role)
    {
        if (role is null) return;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Factory.TokenFor(UserId, role));
    }

    private static HttpRequestMessage Request(string method, string url, string? body)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return request;
    }
}
