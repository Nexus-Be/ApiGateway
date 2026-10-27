namespace Nexus.Gateway.Tests;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

public sealed class NotificationAndLogMockTests
{
    private const string UserId = "11111111-1111-1111-1111-000000000001";

    private static HttpClient CreateClient(Factory app, string role = "buyer")
    {
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Factory.TokenFor(UserId, role));
        return client;
    }

    // =========================================================================
    // 1. Notification Controller Tests (NOTI-01 to NOTI-16)
    // =========================================================================

    [Fact]
    public async Task RegisterDeviceToken_ValidRequest_ReturnsOkWithCamelCase()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "buyer");

        var response = await client.PostAsJsonAsync("/api/v1/notifications/devices", new
        {
            platform = "WEB",
            token = "fcm-test-token-12345"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Mock", response.Headers.GetValues("X-Gateway-Mode").Single());

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(200, root.GetProperty("statusCode").GetInt32());

        var data = root.GetProperty("data");
        Assert.True(data.TryGetProperty("deviceId", out _));
        Assert.True(data.TryGetProperty("platform", out var platformProp));
        Assert.Equal("WEB", platformProp.GetString());
        Assert.True(data.TryGetProperty("isActive", out var activeProp));
        Assert.True(activeProp.GetBoolean());
    }

    [Fact]
    public async Task RegisterDeviceToken_InvalidPlatform_ThrowsValidationException()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "buyer");

        var response = await client.PostAsJsonAsync("/api/v1/notifications/devices", new
        {
            platform = "WINDOWS_PHONE",
            token = "fcm-test-token-12345"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("VAL_INVALID_INPUT", doc.RootElement.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task RevokeDeviceToken_ReturnsNoContent()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "buyer");

        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/notifications/devices")
        {
            Content = JsonContent.Create(new { token = "fcm-test-token-12345" })
        };
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task GetNotificationPreferences_ReturnsOkWithCamelCase()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "buyer");

        var response = await client.GetAsync("/api/v1/notifications/preferences");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());

        var data = root.GetProperty("data");
        Assert.True(data.TryGetProperty("items", out var items));
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
        Assert.True(items.GetArrayLength() > 0);

        var first = items[0];
        Assert.True(first.TryGetProperty("notificationType", out _));
        Assert.True(first.TryGetProperty("channelCode", out _));
        Assert.True(first.TryGetProperty("isEnabled", out _));
    }

    [Fact]
    public async Task UpdateNotificationPreferences_ReturnsOkWithUpdatedCount()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "buyer");

        var response = await client.PutAsJsonAsync("/api/v1/notifications/preferences", new
        {
            preferences = new[]
            {
                new { notificationType = "ORDER_PAID", channelCode = "EMAIL", isEnabled = true },
                new { notificationType = "PROMOTION", channelCode = "PUSH", isEnabled = false }
            }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = doc.RootElement.GetProperty("data");
        Assert.True(data.TryGetProperty("updatedCount", out var countProp));
        Assert.True(countProp.GetInt32() >= 0);
    }

    [Fact]
    public async Task GetMyNotifications_ReturnsPagedNotifications()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "buyer");

        var response = await client.GetAsync("/api/v1/notifications/me?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());

        var data = root.GetProperty("data");
        Assert.True(data.TryGetProperty("unreadCount", out _));
        Assert.True(data.TryGetProperty("items", out var items));
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
        Assert.True(items.GetArrayLength() > 0);

        var first = items[0];
        Assert.True(first.TryGetProperty("notificationType", out _));
        Assert.True(first.TryGetProperty("title", out _));
        Assert.True(first.TryGetProperty("isRead", out _));
    }

    [Fact]
    public async Task MarkNotificationsAsRead_ReturnsOk()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "buyer");

        var response = await client.PatchAsJsonAsync("/api/v1/notifications/me/read", new
        {
            notificationIds = new[] { Guid.Parse("11111111-1111-1111-1111-000000000001") },
            markAll = false
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = doc.RootElement.GetProperty("data");
        Assert.True(data.TryGetProperty("updatedCount", out _));
        Assert.True(data.TryGetProperty("unreadCount", out _));
    }

    [Fact]
    public async Task GetNotificationChannel_Admin_ReturnsOk()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "admin");

        var response = await client.GetAsync("/api/v1/admin/notifications/channels/EMAIL");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = doc.RootElement.GetProperty("data");
        Assert.True(data.TryGetProperty("code", out var codeProp));
        Assert.Equal("EMAIL", codeProp.GetString());
        Assert.True(data.TryGetProperty("provider", out _));
    }

    [Fact]
    public async Task GetNotificationChannel_Buyer_ReturnsForbidden()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "buyer");

        var response = await client.GetAsync("/api/v1/admin/notifications/channels/EMAIL");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListTemplates_Admin_ReturnsOk()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "admin");

        var response = await client.GetAsync("/api/v1/admin/notifications/templates");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = doc.RootElement.GetProperty("data");
        Assert.True(data.TryGetProperty("items", out var items));
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
    }

    [Fact]
    public async Task SearchNotificationHistory_Admin_ReturnsOk()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "admin");

        var response = await client.GetAsync("/api/v1/admin/notifications");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = doc.RootElement.GetProperty("data");
        Assert.True(data.TryGetProperty("items", out var items));
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
    }

    [Fact]
    public async Task GetEventLogs_Admin_ReturnsOk()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "admin");

        var response = await client.GetAsync("/api/v1/admin/event-logs");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = doc.RootElement.GetProperty("data");
        Assert.True(data.TryGetProperty("items", out var items));
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
    }

    [Fact]
    public async Task GetBrokerHealth_Admin_ReturnsOk()
    {
        using var app = new Factory();
        using var client = CreateClient(app, "admin");

        var response = await client.GetAsync("/api/v1/admin/health/broker");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = doc.RootElement.GetProperty("data");
        Assert.True(data.TryGetProperty("currentStatus", out var statusProp));
        Assert.Equal("UP", statusProp.GetString());
        Assert.True(data.TryGetProperty("history", out _));
    }

}
