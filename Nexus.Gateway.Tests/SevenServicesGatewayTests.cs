using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Nexus.Gateway.Tests;

public sealed class SevenServicesGatewayTests
{
    [Fact]
    public async Task Get_and_update_gateway_modes_runtime()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Factory.TokenFor("gateway-admin", "admin"));

        // 1. Get all modes
        var getResp = await client.GetAsync("/api/v1/gateway/modes");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var getContent = await getResp.Content.ReadAsStringAsync();
        using var getDoc = JsonDocument.Parse(getContent);

        Assert.True(getDoc.RootElement.GetProperty("success").GetBoolean());
        var data = getDoc.RootElement.GetProperty("data");

        Assert.Contains(data.GetProperty("gatewayMode").GetString(), new[] { "Standard", "Bypass" });
        var services = data.GetProperty("services");

        // Verify all 8 services exist (including wallet)
        foreach (var svc in new[] { "catalog", "commerce", "fulfillment", "users", "auction", "notification", "audit", "wallet" })
        {
            Assert.True(services.TryGetProperty(svc, out var svcObj), $"Missing service in modes: {svc}");
            Assert.Equal("Mock", svcObj.GetProperty("mode").GetString());
        }

        // 2. Update auction mode to Real
        var updateResp = await client.PutAsJsonAsync("/api/v1/gateway/modes/auction", new { mode = "Real" });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);

        // 3. Verify auction mode is now Real
        var checkResp = await client.GetAsync("/api/v1/gateway/modes");
        var checkContent = await checkResp.Content.ReadAsStringAsync();
        using var checkDoc = JsonDocument.Parse(checkContent);
        Assert.Equal("Real", checkDoc.RootElement.GetProperty("data").GetProperty("services").GetProperty("auction").GetProperty("mode").GetString());

        // 4. Reset auction back to Mock
        var resetResp = await client.PutAsJsonAsync("/api/v1/gateway/modes/auction", new { mode = "Mock" });
        Assert.Equal(HttpStatusCode.OK, resetResp.StatusCode);
    }

    [Fact]
    public async Task Gateway_control_plane_requires_a_real_administrator_even_when_bypass_is_enabled()
    {
        using var app = new Factory
        {
            Configure = o => o.AuthMode = "Bypass"
        };
        using var client = app.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/gateway/modes")).StatusCode);

        client.DefaultRequestHeaders.Authorization = new("Bearer", "admin");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/gateway/modes")).StatusCode);

        client.DefaultRequestHeaders.Authorization = new("Bearer", Factory.TokenFor("gateway-admin", "admin"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/gateway/modes")).StatusCode);
    }

    [Fact]
    public async Task Client_header_override_mode_per_request()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        // Default mock response has X-Gateway-Mode: Mock
        var defaultResp = await client.GetAsync("/api/v1/catalog/products");
        Assert.Equal(HttpStatusCode.OK, defaultResp.StatusCode);
        Assert.Equal("Mock", defaultResp.Headers.GetValues("X-Gateway-Mode").Single());

        // With header X-Mock-Mode: Real -> Gateway executes Real flow (fails with ServiceUnavailable since no real gRPC is running)
        using var realReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/catalog/products");
        realReq.Headers.Add("X-Mock-Mode", "Real");
        var realResp = await client.SendAsync(realReq);
        Assert.Equal("Real", realResp.Headers.GetValues("X-Gateway-Mode").Single());
        Assert.Contains(realResp.StatusCode, new[] { HttpStatusCode.ServiceUnavailable, HttpStatusCode.BadGateway });

        // With X-Service-Modes: catalog=Mock;auction=Real
        using var mixedReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/catalog/products");
        mixedReq.Headers.Add("X-Service-Modes", "catalog=Mock;auction=Real");
        var mixedResp = await client.SendAsync(mixedReq);
        Assert.Equal("Mock", mixedResp.Headers.GetValues("X-Gateway-Mode").Single());
        Assert.Equal(HttpStatusCode.OK, mixedResp.StatusCode);
    }

    [Fact]
    public async Task Auction_mock_endpoints_work_with_proper_policies()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        // 1. List auctions (Public)
        var listResp = await client.GetAsync("/api/v1/auctions?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var listContent = await listResp.Content.ReadAsStringAsync();
        using var listDoc = JsonDocument.Parse(listContent);
        Assert.True(listDoc.RootElement.GetProperty("success").GetBoolean());
        Assert.True(listDoc.RootElement.GetProperty("data").GetProperty("items").GetArrayLength() > 0);

        // 2. Get single auction (Public)
        var getResp = await client.GetAsync("/api/v1/auctions/44444444-4444-4444-4444-000000000001");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);

        // 3. Create auction (Seller policy required)
        var createBody = new
        {
            productId = Guid.NewGuid().ToString(),
            startingPrice = "1000000.00",
            stepPrice = "50000.00",
            startTime = "2026-10-15T08:00:00Z",
            endTime = "2026-10-16T08:00:00Z",
            title = "Bàn phím cơ hiếm"
        };
        // Anonymous => 401
        var unauthCreate = await client.PostAsJsonAsync("/api/v1/auctions", createBody);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthCreate.StatusCode);

        // Buyer => 403
        client.DefaultRequestHeaders.Authorization = new("Bearer", Factory.TokenFor("buyer-1", "buyer"));
        var forbiddenCreate = await client.PostAsJsonAsync("/api/v1/auctions", createBody);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenCreate.StatusCode);

        // Seller => 201 Created
        client.DefaultRequestHeaders.Authorization = new("Bearer", Factory.TokenFor("seller-1", "seller"));
        var successCreate = await client.PostAsJsonAsync("/api/v1/auctions", createBody);
        Assert.Equal(HttpStatusCode.Created, successCreate.StatusCode);
        var createContent = await successCreate.Content.ReadAsStringAsync();
        using var createDoc = JsonDocument.Parse(createContent);
        Assert.True(createDoc.RootElement.GetProperty("success").GetBoolean());

        // 4. Place bid (Authenticated buyer)
        client.DefaultRequestHeaders.Authorization = new("Bearer", Factory.TokenFor("buyer-1", "buyer"));
        var bidBody = new { amount = "1550000.00" };
        var bidResp = await client.PostAsJsonAsync("/api/v1/auctions/44444444-4444-4444-4444-000000000001/bids", bidBody);
        Assert.Equal(HttpStatusCode.Created, bidResp.StatusCode);

        // 5. List bids (Public)
        client.DefaultRequestHeaders.Authorization = null;
        var bidsResp = await client.GetAsync("/api/v1/auctions/44444444-4444-4444-4444-000000000001/bids");
        Assert.Equal(HttpStatusCode.OK, bidsResp.StatusCode);
    }

    [Fact]
    public async Task Fulfillment_mock_endpoints_work()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        // 1. Inventory by SKU
        var invResp = await client.GetAsync("/api/v1/fulfillment/inventory/SKU-KEYBOARD-RGB-001");
        Assert.Equal(HttpStatusCode.OK, invResp.StatusCode);
        var invContent = await invResp.Content.ReadAsStringAsync();
        using var invDoc = JsonDocument.Parse(invContent);
        Assert.Equal("WH_001", invDoc.RootElement.GetProperty("data").GetProperty("warehouseCode").GetString());

        // 2. Shipping fee quote
        var quoteBody = new
        {
            sellerAddress = "Quận 1, TP.HCM",
            buyerAddress = "Quận Cầu Giấy, Hà Nội",
            weightGrams = 1200
        };
        var quoteResp = await client.PostAsJsonAsync("/api/v1/fulfillment/shipping/quote", quoteBody);
        Assert.Equal(HttpStatusCode.OK, quoteResp.StatusCode);
        var quoteContent = await quoteResp.Content.ReadAsStringAsync();
        using var quoteDoc = JsonDocument.Parse(quoteContent);
        Assert.Equal("GHN", quoteDoc.RootElement.GetProperty("data").GetProperty("carrier").GetString());

        // 3. Shipment tracking
        var trackResp = await client.GetAsync("/api/v1/fulfillment/shipments/GHN-VN-987654321");
        Assert.Equal(HttpStatusCode.OK, trackResp.StatusCode);
        var trackContent = await trackResp.Content.ReadAsStringAsync();
        using var trackDoc = JsonDocument.Parse(trackContent);
        Assert.Equal("IN_TRANSIT", trackDoc.RootElement.GetProperty("data").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Notification_mock_endpoints_work_with_auth()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        // 1. Unauthorized access
        var unauth = await client.GetAsync("/api/v1/notifications");
        Assert.Equal(HttpStatusCode.Unauthorized, unauth.StatusCode);

        // 2. Authenticated user access
        client.DefaultRequestHeaders.Authorization = new("Bearer", Factory.TokenFor("user-1", "buyer"));

        var notiResp = await client.GetAsync("/api/v1/notifications");
        Assert.Equal(HttpStatusCode.OK, notiResp.StatusCode);
        var notiContent = await notiResp.Content.ReadAsStringAsync();
        using var notiDoc = JsonDocument.Parse(notiContent);
        Assert.True(notiDoc.RootElement.GetProperty("success").GetBoolean());
        Assert.True(notiDoc.RootElement.GetProperty("data").GetProperty("items").GetArrayLength() > 0);

        // 3. Mark read
        var readResp = await client.PutAsync("/api/v1/notifications/66666666-6666-6666-6666-000000000001/read", null);
        Assert.Equal(HttpStatusCode.OK, readResp.StatusCode);
        var readContent = await readResp.Content.ReadAsStringAsync();
        using var readDoc = JsonDocument.Parse(readContent);
        Assert.True(readDoc.RootElement.GetProperty("data").GetProperty("isRead").GetBoolean());

        // 4. Get preferences
        var prefResp = await client.GetAsync("/api/v1/notifications/preferences");
        Assert.Equal(HttpStatusCode.OK, prefResp.StatusCode);

        // 5. Update preferences
        var updatePrefBody = new
        {
            emailEnabled = true,
            pushEnabled = false,
            orderUpdates = true,
            auctionAlerts = false
        };
        var updatePrefResp = await client.PutAsJsonAsync("/api/v1/notifications/preferences", updatePrefBody);
        Assert.Equal(HttpStatusCode.OK, updatePrefResp.StatusCode);
    }
    [Fact]
    public async Task Swagger_and_mock_auth_allows_calling_getme_without_token_when_enabled()
    {
        using var app = new Factory
        {
            Configure = o =>
            {
                o.AuthMode = "Bypass";
            }
        };
        using var client = app.CreateClient();

        // Calling /users/me without any token in Swagger mock dev mode -> returns 200 OK!
        var resp = await client.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var content = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("11111111-1111-1111-1111-000000000004", doc.RootElement.GetProperty("data").GetProperty("id").GetString());
    }

    [Fact]
    public async Task Swagger_authorize_with_simple_role_names_works()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        // 1. Authorize as "seller" by typing "seller" in Swagger
        client.DefaultRequestHeaders.Authorization = new("Bearer", "seller");
        var sellerResp = await client.GetAsync("/api/v1/seller/inventory");
        Assert.Equal(HttpStatusCode.OK, sellerResp.StatusCode);

        // 2. Authorize as "admin" by typing "admin" in Swagger
        client.DefaultRequestHeaders.Authorization = new("Bearer", "admin");
        var adminResp = await client.GetAsync("/api/v1/admin/roles");
        Assert.Equal(HttpStatusCode.OK, adminResp.StatusCode);

        // 3. Passing mock login token also works
        client.DefaultRequestHeaders.Authorization = new("Bearer", "eyJhbGciOiJIUzI1NiJ9.mock-token-for-development-only");
        var meResp = await client.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.OK, meResp.StatusCode);
    }

    [Fact]
    public async Task Independent_single_service_testing_works_without_user_service()
    {
        // Chi co 1 service (catalog) chay gRPC that, user service khong chay (Mock)
        var catalogAddress = await Stub.StartAsync<TestCatalogStub>();
        try
        {
            using var app = new Factory
            {
                Configure = o =>
                {
                    o.Services["catalog"].Mode = "Real";
                    o.Services["catalog"].Address = catalogAddress;
                    o.Services["users"].Mode = "Mock";
                    o.AuthMode = "Bypass";
                }
            };
            using var client = app.CreateClient();

            // 1. Service catalog chay Real: goi ra du lieu that tu stub
            var prodResp = await client.GetAsync("/api/v1/catalog/products");
            Assert.Equal(HttpStatusCode.OK, prodResp.StatusCode);
            Assert.Equal("Real", prodResp.Headers.GetValues("X-Gateway-Mode").Single());

            // 2. Service users chay Mock: goi /users/me ma khong can server users/identity chay
            var meResp = await client.GetAsync("/api/v1/users/me");
            Assert.Equal(HttpStatusCode.OK, meResp.StatusCode);
            Assert.Equal("Mock", meResp.Headers.GetValues("X-Gateway-Mode").Single());
        }
        finally
        {
            await Stub.StopAsync(catalogAddress);
        }
    }
    [Fact]
    public async Task Mock_service_automatically_adapts_auth_for_any_policy_when_mocking()
    {
        using var app = new Factory
        {
            Configure = o =>
            {
                o.AuthMode = "Bypass";
            }
        };
        using var client = app.CreateClient();

        // 1. Endpoint requiring 'authenticated': /users/me without token -> 200 OK
        var r1 = await client.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);

        // 2. Endpoint requiring 'seller': /seller/inventory without token -> 200 OK
        var r2 = await client.GetAsync("/api/v1/seller/inventory");
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);

        // 3. Endpoint requiring 'admin': /admin/roles without token -> 200 OK
        var r3 = await client.GetAsync("/api/v1/admin/roles");
        Assert.Equal(HttpStatusCode.OK, r3.StatusCode);
    }

    [Fact]
    public async Task Mock_service_strictly_enforces_auth_when_X_Mock_Enforce_Auth_is_sent()
    {
        using var app = new Factory
        {
            Configure = o =>
            {
                o.AuthMode = "Bypass";
            }
        };
        using var client = app.CreateClient();

        // 1. Without token + X-Mock-Enforce-Auth: true -> 401 Unauthorized
        using var unauthReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/seller/inventory");
        unauthReq.Headers.Add("X-Mock-Enforce-Auth", "true");
        var unauthResp = await client.SendAsync(unauthReq);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthResp.StatusCode);

        // 2. With wrong role (buyer on seller endpoint) + X-Mock-Enforce-Auth: true -> 403 Forbidden
        using var forbiddenReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/seller/inventory");
        forbiddenReq.Headers.Add("X-Mock-Enforce-Auth", "true");
        forbiddenReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Factory.TokenFor("real-user", "buyer"));
        var forbiddenResp = await client.SendAsync(forbiddenReq);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResp.StatusCode);
    }
    [Fact]
    public async Task Gateway_Standard_mode_strictly_requires_login_as_normal()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        // 1. In Standard mode (default), calling without token returns 401 Unauthorized!
        var unauthResp = await client.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthResp.StatusCode);

        // 2. Calling with valid mock/login token returns 200 OK!
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "eyJhbGciOiJIUzI1NiJ9.mock-token-for-development-only");
        var authResp = await client.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.OK, authResp.StatusCode);

        // 3. Calling admin endpoint with buyer role returns 403 Forbidden!
        var forbiddenResp = await client.GetAsync("/api/v1/admin/roles");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResp.StatusCode);
    }

    [Fact]
    public async Task Gateway_Bypass_mode_skips_auth_check_and_runs_independently()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        // 1. In Standard mode (no token) -> 401 Unauthorized
        var strictCheck = await client.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, strictCheck.StatusCode);

        // 2. With X-Gateway-Auth: Bypass -> ALL endpoints run independently and DO NOT check auth!
        client.DefaultRequestHeaders.Add("X-Gateway-Auth", "Bypass");

        // /users/me without token -> 200 OK!
        var r1 = await client.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);

        // /seller/inventory without token -> 200 OK!
        var r2 = await client.GetAsync("/api/v1/seller/inventory");
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);

        // /admin/roles without token -> 200 OK!
        var r3 = await client.GetAsync("/api/v1/admin/roles");
        Assert.Equal(HttpStatusCode.OK, r3.StatusCode);

        // 3. Remove header -> returns to Standard mode
        client.DefaultRequestHeaders.Remove("X-Gateway-Auth");
        var backToStrict = await client.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, backToStrict.StatusCode);
    }
    [Fact]
    public async Task Wallet_mock_endpoints_work_with_proper_policies()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        // 1. Calling /api/v1/wallet/balance without token -> 401 Unauthorized
        var unauth = await client.GetAsync("/api/v1/wallet/balance");
        Assert.Equal(HttpStatusCode.Unauthorized, unauth.StatusCode);

        // 2. Calling with authenticated token -> 200 OK with wallet balance
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Factory.TokenFor("user-1", "buyer"));
        var balResp = await client.GetAsync("/api/v1/wallet/balance");
        Assert.Equal(HttpStatusCode.OK, balResp.StatusCode);
        Assert.Equal("Mock", balResp.Headers.GetValues("X-Gateway-Mode").Single());

        var balContent = await balResp.Content.ReadAsStringAsync();
        using var balDoc = JsonDocument.Parse(balContent);
        Assert.True(balDoc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("5000000.00", balDoc.RootElement.GetProperty("data").GetProperty("balance").GetString());

        // 3. Deposit money into wallet
        var depositBody = new
        {
            amount = "1000000.00",
            currency = "VND",
            paymentMethod = "BANK_TRANSFER"
        };
        var depResp = await client.PostAsJsonAsync("/api/v1/wallet/deposit", depositBody);
        Assert.Equal(HttpStatusCode.OK, depResp.StatusCode);

        var depContent = await depResp.Content.ReadAsStringAsync();
        using var depDoc = JsonDocument.Parse(depContent);
        Assert.Equal("6000000.00", depDoc.RootElement.GetProperty("data").GetProperty("balanceAfter").GetString());

        // 4. Withdraw money from wallet
        var withdrawBody = new
        {
            amount = "500000.00",
            currency = "VND",
            bankAccount = "0123456789 (Vietcombank)"
        };
        var witResp = await client.PostAsJsonAsync("/api/v1/wallet/withdraw", withdrawBody);
        Assert.Equal(HttpStatusCode.OK, witResp.StatusCode);

        var witContent = await witResp.Content.ReadAsStringAsync();
        using var witDoc = JsonDocument.Parse(witContent);
        Assert.Equal("4500000.00", witDoc.RootElement.GetProperty("data").GetProperty("balanceAfter").GetString());

        // 5. Transfer money to another recipient
        var transferBody = new
        {
            recipientUserId = "22222222-2222-2222-2222-000000000002",
            amount = "200000.00",
            note = "Chuyển tiền mua bàn phím"
        };
        var trfResp = await client.PostAsJsonAsync("/api/v1/wallet/transfer", transferBody);
        Assert.Equal(HttpStatusCode.OK, trfResp.StatusCode);

        // 6. List wallet transactions
        var txResp = await client.GetAsync("/api/v1/wallet/transactions?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, txResp.StatusCode);
        var txContent = await txResp.Content.ReadAsStringAsync();
        using var txDoc = JsonDocument.Parse(txContent);
        Assert.True(txDoc.RootElement.GetProperty("data").GetProperty("items").GetArrayLength() > 0);

        // 7. Declarative mock endpoint: /api/v1/wallet/quick-summary
        var summaryResp = await client.GetAsync("/api/v1/wallet/quick-summary");
        Assert.Equal(HttpStatusCode.OK, summaryResp.StatusCode);
        var summaryContent = await summaryResp.Content.ReadAsStringAsync();
        using var summaryDoc = JsonDocument.Parse(summaryContent);
        Assert.Equal("5000000.00", summaryDoc.RootElement.GetProperty("data").GetProperty("balance").GetString());
    }
}

