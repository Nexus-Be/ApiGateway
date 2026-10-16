using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Nexus.Gateway.Tests;

public sealed class MoneyPrecisionHttpTests
{
    private const string UserId = "11111111-1111-1111-1111-111111111111";
    private const string AuctionId = "44444444-4444-4444-4444-000000000001";
    private const string ProductId = "22222222-2222-2222-2222-000000000001";

    public static IEnumerable<object[]> MoneyFields()
    {
        yield return ["/gateway/api/v1/wallet/deposit", "buyer", "amount"];
        yield return ["/gateway/api/v1/wallet/withdraw", "buyer", "amount"];
        yield return ["/gateway/api/v1/wallet/transfer", "buyer", "amount"];
        yield return ["/gateway/api/v1/auctions", "seller", "startingPrice"];
        yield return ["/gateway/api/v1/auctions", "seller", "stepPrice"];
        yield return [$"/gateway/api/v1/auctions/{AuctionId}/bids", "buyer", "amount"];
        yield return ["/gateway/api/v1/seller/products", "seller", "price"];
        yield return [$"/gateway/api/v1/seller/products/{ProductId}", "seller", "price"];
    }

    [Theory]
    [MemberData(nameof(MoneyFields))]
    public async Task Rejects_unrepresentable_money_without_dispatch(string path, string role, string field)
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Factory.TokenFor(UserId, role));

        foreach (var amount in new[] { "0.001", "1.005" })
        {
            using var response = await SendMoneyAsync(client, path, field, amount);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("VAL_INVALID_INPUT", document.RootElement.GetProperty("errorCode").GetString());
        }
    }

    [Theory]
    [MemberData(nameof(MoneyFields))]
    public async Task Accepts_one_cent_at_every_money_entry_point(string path, string role, string field)
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Factory.TokenFor(UserId, role));

        using var response = await SendMoneyAsync(client, path, field, "0.01");
        Assert.Equal(path == "/gateway/api/v1/auctions" || path.EndsWith("/bids", StringComparison.Ordinal)
            ? HttpStatusCode.Created : HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("Mock", response.Headers.GetValues("X-Gateway-Mode").Single());
    }

    [Theory]
    [InlineData("N")]
    [InlineData("B")]
    public async Task Transfer_rejects_self_recipient_in_alternate_guid_format(string guidFormat)
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Factory.TokenFor(UserId, "buyer"));
        var alternate = Guid.Parse(UserId).ToString(guidFormat);

        using var response = await client.PostAsJsonAsync("/gateway/api/v1/wallet/transfer", new
        {
            recipientUserId = alternate,
            amount = "0.01"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("VAL_INVALID_INPUT", document.RootElement.GetProperty("errorCode").GetString());
    }

    private static Task<HttpResponseMessage> SendMoneyAsync(HttpClient client, string path, string field, string amount)
    {
        var body = new Dictionary<string, object?>
        {
            ["bankAccount"] = "1234567890",
            ["recipientUserId"] = "22222222-2222-2222-2222-222222222222",
            ["productId"] = ProductId,
            ["title"] = "Auction",
            ["startTime"] = "2026-01-01T00:00:00Z",
            ["endTime"] = "2026-01-02T00:00:00Z",
            ["startingPrice"] = "0.01",
            ["stepPrice"] = "0.01",
            ["name"] = "Keyboard",
            ["available"] = 0
        };
        body[field] = amount;
        return path.Contains($"/{ProductId}", StringComparison.Ordinal)
            ? client.PutAsJsonAsync(path, body)
            : client.PostAsJsonAsync(path, body);
    }
}
