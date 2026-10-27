namespace Nexus.Gateway.Tests;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

public sealed class UserMockTests
{
    private const string UserId = "11111111-1111-1111-1111-000000000001";
    private const string SellerId = "22222222-2222-2222-2222-000000000001";
    private const string RegId = "reg_11111111-1111-1111-1111-000000000001";
    private const string ReportId = "report_11111111-1111-1111-1111-000000000001";
    private const string ReviewId = "review_11111111-1111-1111-1111-000000000001";

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

    private static HttpClient CreateClient(Factory app, string role = "buyer", string subject = "user-1")
    {
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor(subject, role));
        return client;
    }

    // ------------------------------------------------------------------ Auth additions & aliases

    [Fact]
    public async Task RefreshToken_ReturnsNewTokens()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken = "sample-refresh-token"
        });
        using var doc = await OkJson(response);

        AssertSuccessEnvelope(doc, 200);
        var data = doc.RootElement.GetProperty("data");
        Assert.True(data.TryGetProperty("accessToken", out _));
        Assert.True(data.TryGetProperty("refreshToken", out _));
    }

    [Fact]
    public async Task ForgotPassword_ReturnsSuccess()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/password/forgot", new
        {
            email = "user@example.com"
        });
        using var doc = await OkJson(response);

        AssertSuccessEnvelope(doc, 200);
        Assert.Equal("user@example.com", doc.RootElement.GetProperty("data").GetProperty("email").GetString());
    }

    [Fact]
    public async Task ResetPassword_ReturnsSuccess()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var response = await client.PutAsJsonAsync("/api/v1/auth/password/reset", new
        {
            email = "user@example.com",
            otpCode = "123456",
            newPassword = "NewStrongPassword123!",
            confirmPassword = "NewStrongPassword123!"
        });
        using var doc = await OkJson(response);

        AssertSuccessEnvelope(doc, 200);
    }

    [Fact]
    public async Task ChangePassword_ReturnsSuccess()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "buyer");

        var response = await client.PatchAsJsonAsync("/api/v1/auth/password", new
        {
            oldPassword = "OldPassword123!",
            newPassword = "NewPassword123!",
            confirmPassword = "NewPassword123!"
        });
        using var doc = await OkJson(response);

        AssertSuccessEnvelope(doc, 200);
    }

    [Fact]
    public async Task Logout_ReturnsSuccess()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "buyer");

        var response = await client.PostAsJsonAsync("/api/v1/auth/logout", new { });
        using var doc = await OkJson(response);

        AssertSuccessEnvelope(doc, 200);
    }

    [Fact]
    public async Task LogoutAll_ReturnsCountRevoked()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "buyer");

        var response = await client.PostAsJsonAsync("/api/v1/auth/logout-all", new { });
        using var doc = await OkJson(response);

        AssertSuccessEnvelope(doc, 200);
        Assert.True(doc.RootElement.GetProperty("data").GetProperty("countRevoked").GetInt32() > 0);
    }

    [Fact]
    public async Task OtpVerifyAndResend_RouteAliasesWork()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        // Check new contract routes
        var verifyRes = await client.PostAsJsonAsync("/api/v1/auth/otp/verify", new
        {
            email = "test@example.com",
            otp = "123456"
        });
        Assert.Equal(HttpStatusCode.OK, verifyRes.StatusCode);

        var resendRes = await client.PostAsJsonAsync("/api/v1/auth/otp/resend", new
        {
            email = "test@example.com"
        });
        Assert.Equal(HttpStatusCode.OK, resendRes.StatusCode);


    }

    // ------------------------------------------------------------------ Me & User Profile

    [Fact]
    public async Task GetSessions_ReturnsList()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "buyer");

        var response = await client.GetAsync("/api/v1/me/sessions");
        using var doc = await OkJson(response);

        AssertSuccessEnvelope(doc, 200);
        var items = doc.RootElement.GetProperty("data").GetProperty("items");
        Assert.True(items.GetArrayLength() > 0);
    }

    [Fact]
    public async Task RevokeSession_Returns204NoContent()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "buyer");

        var response = await client.DeleteAsync("/api/v1/me/sessions/session_002");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_RouteAliasesWork()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "buyer");

        var res1 = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, res1.StatusCode);


    }

    [Fact]
    public async Task UpdateMe_ReturnsUpdatedProfile()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "buyer");

        var response = await client.PatchAsJsonAsync("/api/v1/me", new
        {
            firstName = "Alex",
            lastName = "Rivers"
        });
        using var doc = await OkJson(response);

        AssertSuccessEnvelope(doc, 200);
        Assert.Equal("Alex", doc.RootElement.GetProperty("data").GetProperty("firstName").GetString());
    }

    [Fact]
    public async Task GetMyAvatar_ReturnsAvatarUrl()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "buyer");

        var response = await client.GetAsync("/api/v1/me/avatar");
        using var doc = await OkJson(response);

        AssertSuccessEnvelope(doc, 200);
        Assert.True(doc.RootElement.GetProperty("data").TryGetProperty("avatarUrl", out _));
    }

    [Fact]
    public async Task UploadMyAvatar_ReturnsNewAvatarUrl()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "buyer");

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent("fake-png-bytes"u8.ToArray());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "File", "avatar.png");

        var response = await client.PostAsync("/api/v1/me/avatar", content);
        using var doc = await OkJson(response);

        AssertSuccessEnvelope(doc, 200);
        Assert.True(doc.RootElement.GetProperty("data").TryGetProperty("avatarUrl", out _));
    }

    [Fact]
    public async Task GetMyReputation_ReturnsScore()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "buyer");

        var response = await client.GetAsync("/api/v1/me/reputation");
        using var doc = await OkJson(response);

        AssertSuccessEnvelope(doc, 200);
        Assert.True(doc.RootElement.GetProperty("data").GetProperty("reputationScore").GetInt32() >= 0);
    }

    [Fact]
    public async Task GetMyPenalties_ReturnsPagedEnvelope()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "buyer");

        var response = await client.GetAsync("/api/v1/me/penalties?page=1&limit=20");
        using var doc = await OkJson(response);

        AssertSuccessEnvelope(doc, 200);
        var data = doc.RootElement.GetProperty("data");
        Assert.True(data.GetProperty("items").GetArrayLength() > 0);
        Assert.Equal(1, data.GetProperty("pagination").GetProperty("pageIndex").GetInt32());
    }

    // ------------------------------------------------------------------ Public Queries

    [Fact]
    public async Task GetPublicProfile_ReturnsProfile()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var response = await client.GetAsync($"/api/v1/user/{UserId}");
        using var doc = await OkJson(response);

        AssertSuccessEnvelope(doc, 200);
        Assert.Equal(UserId, doc.RootElement.GetProperty("data").GetProperty("id").GetString());
    }

    [Fact]
    public async Task GetPublicAvatar_ReturnsAvatar()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var response = await client.GetAsync($"/api/v1/user/{UserId}/avatar");
        using var doc = await OkJson(response);

        AssertSuccessEnvelope(doc, 200);
        Assert.True(doc.RootElement.GetProperty("data").TryGetProperty("avatarUrl", out _));
    }

    [Fact]
    public async Task GetSellerPublicProfile_ReturnsShop()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var response = await client.GetAsync($"/api/v1/seller/{SellerId}");
        using var doc = await OkJson(response);

        AssertSuccessEnvelope(doc, 200);
        Assert.Equal(SellerId, doc.RootElement.GetProperty("data").GetProperty("sellerId").GetString());
    }

    [Fact]
    public async Task GetUserReputationAndHistory_ReturnsData()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "buyer");

        var repRes = await client.GetAsync($"/api/v1/user/{UserId}/reputation");
        using var repDoc = await OkJson(repRes);
        AssertSuccessEnvelope(repDoc, 200);

        var histRes = await client.GetAsync($"/api/v1/user/{UserId}/reputation/history?page=1&limit=20");
        using var histDoc = await OkJson(histRes);
        AssertSuccessEnvelope(histDoc, 200);
        Assert.Equal(1, histDoc.RootElement.GetProperty("data").GetProperty("pagination").GetProperty("pageIndex").GetInt32());
    }

    [Fact]
    public async Task GetUserReviews_ReturnsPagedReviews()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var response = await client.GetAsync($"/api/v1/user/{UserId}/reviews?page=1&limit=20");
        using var doc = await OkJson(response);

        AssertSuccessEnvelope(doc, 200);
        var data = doc.RootElement.GetProperty("data");
        Assert.True(data.GetProperty("items").GetArrayLength() > 0);
    }

    // ------------------------------------------------------------------ Reviews & Reports

    [Fact]
    public async Task CreateReview_Returns201Created()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "buyer");

        var response = await client.PostAsJsonAsync("/api/v1/reviews", new
        {
            transactionType = "ORDER",
            transactionId = "order_123",
            targetUserId = UserId,
            rating = 5,
            comment = "Excellent experience!"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var doc = Parse(await response.Content.ReadAsStringAsync());
        AssertSuccessEnvelope(doc, 201);
    }

    [Fact]
    public async Task CreateReport_Returns201Created()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "buyer");

        var response = await client.PostAsJsonAsync("/api/v1/users/reports", new
        {
            reportedUserId = UserId,
            violationType = "COUNTERFEIT",
            description = "Counterfeit item"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var doc = Parse(await response.Content.ReadAsStringAsync());
        AssertSuccessEnvelope(doc, 201);
    }

    [Fact]
    public async Task GetMyReports_ReturnsPagedReports()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "buyer");

        var response = await client.GetAsync("/api/v1/me/reports?page=1&limit=20");
        using var doc = await OkJson(response);

        AssertSuccessEnvelope(doc, 200);
        Assert.Equal(1, doc.RootElement.GetProperty("data").GetProperty("pagination").GetProperty("pageIndex").GetInt32());
    }

    // ------------------------------------------------------------------ Seller Registration & Shop

    [Fact]
    public async Task SellerRegistration_FlowWorks()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "buyer");

        // 1. Submit registration
        var regRes = await client.PostAsJsonAsync("/api/v1/seller/registration", new
        {
            shopName = "Test Shop",
            businessEmail = "seller@example.com",
            hotline = "+84901234567",
            identityCardNumber = "079090001234"
        });
        Assert.Equal(HttpStatusCode.Created, regRes.StatusCode);

        // 2. Resend OTP
        var resendRes = await client.PostAsJsonAsync("/api/v1/seller/registration/otp/resend", new
        {
            registrationId = RegId,
            businessEmail = "seller@example.com"
        });
        Assert.Equal(HttpStatusCode.OK, resendRes.StatusCode);

        // 3. Verify OTP
        var verifyRes = await client.PostAsJsonAsync("/api/v1/seller/otp/verify", new
        {
            registrationId = RegId,
            otpCode = "123456"
        });
        Assert.Equal(HttpStatusCode.OK, verifyRes.StatusCode);

        // 4. Check status
        var statusRes = await client.GetAsync("/api/v1/seller/registration");
        Assert.Equal(HttpStatusCode.OK, statusRes.StatusCode);
    }

    [Fact]
    public async Task SellerProfile_GetAndPatch()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "seller");

        var getRes = await client.GetAsync("/api/v1/seller/profile");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

        var patchRes = await client.PatchAsJsonAsync("/api/v1/seller/profile", new
        {
            shopName = "Updated Shop Name"
        });
        Assert.Equal(HttpStatusCode.OK, patchRes.StatusCode);
    }

    // ------------------------------------------------------------------ Admin Operations

    [Fact]
    public async Task Admin_SellerRegistrations_ListAndApproveReject()
    {
        using var app = new Factory();
        using var staffClient = CreateClient(app, "staff");

        var listRes = await staffClient.GetAsync("/api/v1/admin/seller-registrations?page=1&limit=20");
        using var listDoc = await OkJson(listRes);
        AssertSuccessEnvelope(listDoc, 200);

        var detailRes = await staffClient.GetAsync($"/api/v1/admin/seller-registrations/{RegId}");
        using var detailDoc = await OkJson(detailRes);
        AssertSuccessEnvelope(detailDoc, 200);

        var approveRes = await staffClient.PutAsJsonAsync($"/api/v1/admin/seller-registrations/{RegId}/approve", new
        {
            note = "All docs valid"
        });
        Assert.Equal(HttpStatusCode.OK, approveRes.StatusCode);

        var rejectRes = await staffClient.PutAsJsonAsync($"/api/v1/admin/seller-registrations/{RegId}/reject", new
        {
            rejectionReason = "Identity card image unclear"
        });
        Assert.Equal(HttpStatusCode.OK, rejectRes.StatusCode);
    }

    [Fact]
    public async Task Admin_ReportsAndReviews_Moderation()
    {
        using var app = new Factory();
        using var staffClient = CreateClient(app, "staff");

        var listRes = await staffClient.GetAsync("/api/v1/admin/reports?page=1&limit=20");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);

        var resolveRes = await staffClient.PutAsJsonAsync($"/api/v1/admin/reports/{ReportId}/resolve", new
        {
            action = "PENALTY_APPLIED",
            pointsDeducted = 10,
            resolutionNote = "Penalized seller"
        });
        Assert.Equal(HttpStatusCode.OK, resolveRes.StatusCode);

        var dismissRes = await staffClient.PutAsJsonAsync($"/api/v1/admin/reports/{ReportId}/dismiss", new
        {
            reason = "Not enough evidence"
        });
        Assert.Equal(HttpStatusCode.OK, dismissRes.StatusCode);

        var hideReviewRes = await staffClient.PutAsJsonAsync($"/api/v1/admin/reviews/{ReviewId}/hide", new
        {
            reason = "Inappropriate language"
        });
        Assert.Equal(HttpStatusCode.OK, hideReviewRes.StatusCode);
    }

    [Fact]
    public async Task Admin_Users_CrudAndActions()
    {
        using var app = new Factory();
        using var adminClient = CreateClient(app, "admin");

        // 1. List users (/user)
        var list1 = await adminClient.GetAsync("/api/v1/admin/user?page=1&limit=20");
        Assert.Equal(HttpStatusCode.OK, list1.StatusCode);

        // 2. Create user (201)
        var createRes = await adminClient.PostAsJsonAsync("/api/v1/admin/user", new
        {
            email = "new.staff@example.com",
            password = "Password123!",
            firstName = "John",
            lastName = "Doe"
        });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        // 3. Detail
        var detailRes = await adminClient.GetAsync($"/api/v1/admin/user/{UserId}");
        Assert.Equal(HttpStatusCode.OK, detailRes.StatusCode);

        // 4. Update
        var updateRes = await adminClient.PutAsJsonAsync($"/api/v1/admin/user/{UserId}", new
        {
            firstName = "Johnny",
            reason = "Customer support request"
        });
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        // 5. Delete (204)
        var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/admin/user/{UserId}")
        {
            Content = JsonContent.Create(new { reason = "GDPR delete request" })
        };
        var deleteRes = await adminClient.SendAsync(req);
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);

        // 6. Ban & Unban
        var banRes = await adminClient.PatchAsJsonAsync($"/api/v1/admin/user/{UserId}/ban", new
        {
            reason = "Fraudulent behavior",
            banDurationDays = 30
        });
        Assert.Equal(HttpStatusCode.OK, banRes.StatusCode);

        var unbanRes = await adminClient.PatchAsJsonAsync($"/api/v1/admin/user/{UserId}/unban", new
        {
            reason = "Appeal approved"
        });
        Assert.Equal(HttpStatusCode.OK, unbanRes.StatusCode);

        // 7. Roles & Reputation
        var rolesRes = await adminClient.PutAsJsonAsync($"/api/v1/admin/user/{UserId}/roles", new
        {
            roleCodes = new[] { "buyer", "seller" },
            reason = "Manual upgrade"
        });
        Assert.Equal(HttpStatusCode.OK, rolesRes.StatusCode);

        var repRes = await adminClient.PatchAsJsonAsync($"/api/v1/admin/user/{UserId}/reputation", new
        {
            delta = 10,
            reason = "Good dispute conduct"
        });
        Assert.Equal(HttpStatusCode.OK, repRes.StatusCode);
    }

    [Fact]
    public async Task Admin_RolesPermissionsAuditConfigs()
    {
        using var app = new Factory();
        using var adminClient = CreateClient(app, "admin");

        var rolesRes = await adminClient.GetAsync("/api/v1/admin/roles");
        Assert.Equal(HttpStatusCode.OK, rolesRes.StatusCode);

        var permRes = await adminClient.GetAsync("/api/v1/admin/permissions");
        Assert.Equal(HttpStatusCode.OK, permRes.StatusCode);

        var updatePermRes = await adminClient.PutAsJsonAsync("/api/v1/admin/roles/3/permissions", new
        {
            permissionIds = new[] { 1, 2, 3 },
            description = "Updated permissions"
        });
        Assert.Equal(HttpStatusCode.OK, updatePermRes.StatusCode);

        var auditRes = await adminClient.GetAsync("/api/v1/admin/audit-logs?page=1&limit=20");
        Assert.Equal(HttpStatusCode.OK, auditRes.StatusCode);

        var configRes = await adminClient.GetAsync("/api/v1/admin/configs");
        Assert.Equal(HttpStatusCode.OK, configRes.StatusCode);

        var updateCfgRes = await adminClient.PutAsJsonAsync("/api/v1/admin/configs", new
        {
            key = "user.reputation.min_score_to_bid",
            value = "60",
            reason = "Higher threshold required"
        });
        Assert.Equal(HttpStatusCode.OK, updateCfgRes.StatusCode);
    }

    // ------------------------------------------------------------------ Validation guards

    [Theory]
    [InlineData("/api/v1/user/invalid-uuid")]
    [InlineData("/api/v1/user/invalid-uuid/avatar")]
    [InlineData("/api/v1/seller/invalid-uuid")]
    [InlineData("/api/v1/user/invalid-uuid/reputation")]
    public async Task MalformedIdentifiers_Return400(string path)
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var doc = Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("VAL_INVALID_INPUT", doc.RootElement.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task InvalidPagination_Returns400()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "buyer");

        var response = await client.GetAsync("/api/v1/me/penalties?page=0&limit=200");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var doc = Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("VAL_INVALID_INPUT", doc.RootElement.GetProperty("errorCode").GetString());
    }
}
