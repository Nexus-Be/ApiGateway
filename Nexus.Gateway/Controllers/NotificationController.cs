namespace Nexus.Gateway.Controllers;

using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.Gateway.Common;
using Nexus.Gateway.Dispatching;
using Nexus.Gateway.Dtos.Notification;
using Nexus.Gateway.Security;

/// <summary>
/// Notification surface (NOTI-01 to NOTI-16).
/// Endpoints follow flow_mock_excel.xlsx and Functions.xlsx:
/// User/Client routes at /api/v1/notifications/* and Admin routes at /api/v1/admin/notifications/* and /api/v1/admin/*.
/// </summary>
[ApiController]
[Route("api/v1")]
[BackendTag("notification")]
[EnableRateLimiting("api")]
public sealed class NotificationController : ControllerBase
{
    private const string Service = "notification";

    private static readonly MessageParser<Struct> SeedParser = Struct.Parser;

    private readonly IGatewayDispatcher _dispatcher;

    public NotificationController(IGatewayDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    private static AsyncUnaryCall<Struct> PendingRealCall(CallOptions _) =>
        throw new NotSupportedException("The notification gRPC contract is not wired yet.");

    /// <summary>Đăng ký hoặc làm mới device token (NOTI-01)</summary>
    [HttpPost("notifications/devices")]
    [Authorize(Policy = GatewayPolicies.Authenticated)]
    public Task<IResult> RegisterDeviceToken([FromBody] RegisterDeviceTokenRequest body)
    {
        if (string.IsNullOrWhiteSpace(body.Platform) ||
            !(body.Platform.Equals("WEB", StringComparison.OrdinalIgnoreCase) ||
              body.Platform.Equals("IOS", StringComparison.OrdinalIgnoreCase) ||
              body.Platform.Equals("ANDROID", StringComparison.OrdinalIgnoreCase)))
        {
            throw new GatewayInputException("Platform must be WEB, IOS, or ANDROID.", "Platform");
        }

        if (string.IsNullOrWhiteSpace(body.Token) || body.Token.Length > 2048)
        {
            throw new GatewayInputException("Device token cannot be empty and cannot exceed 2048 characters.", "Token");
        }

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "device-register", PendingRealCall, SeedParser,
            successMessage: "Device token registered successfully.");
    }

    /// <summary>Thu hồi hoặc vô hiệu hóa device token qua DELETE body (NOTI-02)</summary>
    [HttpDelete("notifications/devices")]
    [Authorize(Policy = GatewayPolicies.Authenticated)]
    public Task<IResult> RevokeDeviceToken([FromBody] RevokeDeviceTokenRequest body)
    {
        if (string.IsNullOrWhiteSpace(body.Token))
        {
            throw new GatewayInputException("Token is required.", "Token");
        }

        return _dispatcher.DispatchNoContentAsync(
            HttpContext, Service, "device-revoke", PendingRealCall, SeedParser);
    }

    /// <summary>Thu hồi hoặc vô hiệu hóa device token qua route parameter (NOTI-02 alias)</summary>
    [HttpDelete("notifications/devices/{token}")]
    [Authorize(Policy = GatewayPolicies.Authenticated)]
    public Task<IResult> RevokeDeviceTokenByRoute([FromRoute] string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new GatewayInputException("Token is required.", "Token");
        }

        return _dispatcher.DispatchNoContentAsync(
            HttpContext, Service, "device-revoke", PendingRealCall, SeedParser);
    }

    /// <summary>Lấy danh sách cấu hình bật/tắt nhận thông báo của người dùng (NOTI-03)</summary>
    [HttpGet("notifications/preferences")]
    [Authorize(Policy = GatewayPolicies.Authenticated)]
    public Task<IResult> GetMyPreferences()
    {
        return _dispatcher.DispatchAsync(
            HttpContext, Service, "preferences-get", PendingRealCall, SeedParser,
            successMessage: "Notification preferences retrieved successfully.");
    }

    /// <summary>Cập nhật tùy chọn bật/tắt nhận thông báo (NOTI-04)</summary>
    [HttpPut("notifications/preferences")]
    [Authorize(Policy = GatewayPolicies.Authenticated)]
    public Task<IResult> UpdatePreferences([FromBody] UpdatePreferencesRequest body)
    {
        if (body.Preferences == null || body.Preferences.Count == 0)
        {
            throw new GatewayInputException("At least one preference must be provided.", "Preferences");
        }

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "preferences-update", PendingRealCall, SeedParser,
            successMessage: "Notification preferences updated successfully.");
    }

    /// <summary>Lấy danh sách lịch sử thông báo in-app quả chuông (NOTI-05)</summary>
    [HttpGet("notifications/me")]
    [Authorize(Policy = GatewayPolicies.Authenticated)]
    public Task<IResult> GetMyNotifications(
        [FromQuery] bool? isRead = null,
        [FromQuery] string? notificationType = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
    {
        if (pageIndex < 1 || pageSize < 1 || pageSize > 100)
        {
            throw new GatewayInputException("PageIndex must be >= 1 and PageSize between 1 and 100.", "Pagination");
        }

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "notifications-me", PendingRealCall, SeedParser,
            successMessage: "Notifications retrieved successfully.");
    }

    /// <summary>Lấy số lượng thông báo chưa đọc (NOTI-06 alias)</summary>
    [HttpGet("notifications/unread-count")]
    [Authorize(Policy = GatewayPolicies.Authenticated)]
    public Task<IResult> GetUnreadCount()
    {
        return _dispatcher.DispatchAsync(
            HttpContext, Service, "notifications-read", PendingRealCall, SeedParser,
            successMessage: "Unread notification count retrieved successfully.");
    }

    /// <summary>Đánh dấu thông báo đã đọc qua batch body (NOTI-06 / NOTI-08)</summary>
    [HttpPatch("notifications/me/read")]
    [HttpPatch("notifications/read-all")]
    [Authorize(Policy = GatewayPolicies.Authenticated)]
    public Task<IResult> MarkNotificationsAsRead([FromBody] MarkReadRequest body)
    {
        return _dispatcher.DispatchAsync(
            HttpContext, Service, "notifications-read", PendingRealCall, SeedParser,
            successMessage: "Notifications marked as read successfully.");
    }

    /// <summary>Đánh dấu một thông báo cụ thể là đã đọc (NOTI-07)</summary>
    [HttpPatch("notifications/{id:guid}/read")]
    [Authorize(Policy = GatewayPolicies.Authenticated)]
    public Task<IResult> MarkSingleNotificationAsRead([FromRoute] Guid id)
    {
        return _dispatcher.DispatchAsync(
            HttpContext, Service, "notifications-read", PendingRealCall, SeedParser,
            successMessage: "Notification marked as read successfully.");
    }

    /// <summary>Admin xem cấu hình kênh gửi thông báo (NOTI-07 sheet)</summary>
    [HttpGet("admin/notifications/channels/{code}")]
    [Authorize(Policy = GatewayPolicies.Admin)]
    public Task<IResult> GetNotificationChannel([FromRoute] string code)
    {
        return _dispatcher.DispatchAsync(
            HttpContext, Service, "channel-detail", PendingRealCall, SeedParser,
            successMessage: "Notification channel retrieved successfully.");
    }

    /// <summary>Admin cập nhật cấu hình kênh gửi thông báo (NOTI-07 sheet)</summary>
    [HttpPut("admin/notifications/channels/{code}")]
    [Authorize(Policy = GatewayPolicies.Admin)]
    public Task<IResult> UpdateNotificationChannel([FromRoute] string code, [FromBody] UpdateChannelRequest body)
    {
        return _dispatcher.DispatchAsync(
            HttpContext, Service, "channel-detail", PendingRealCall, SeedParser,
            successMessage: "Notification channel updated successfully.");
    }

    /// <summary>Admin xem danh sách mẫu thông báo (NOTI-08 sheet / NOTI-12)</summary>
    [HttpGet("admin/notifications/templates")]
    [Authorize(Policy = GatewayPolicies.Admin)]
    public Task<IResult> GetNotificationTemplates(
        [FromQuery] string? channelCode = null,
        [FromQuery] string? notificationType = null)
    {
        return _dispatcher.DispatchAsync(
            HttpContext, Service, "templates-get", PendingRealCall, SeedParser,
            successMessage: "Notification templates retrieved successfully.");
    }

    /// <summary>Admin tạo mẫu nội dung thông báo mới (NOTI-08 sheet / NOTI-10)</summary>
    [HttpPost("admin/notifications/templates")]
    [Authorize(Policy = GatewayPolicies.Admin)]
    public Task<IResult> CreateNotificationTemplate([FromBody] SaveTemplateRequest body)
    {
        if (string.IsNullOrWhiteSpace(body.Code) ||
            string.IsNullOrWhiteSpace(body.NotificationType) ||
            string.IsNullOrWhiteSpace(body.BodyTemplate))
        {
            throw new GatewayInputException("Code, NotificationType and BodyTemplate are required.", "Template");
        }

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "template-create", PendingRealCall, SeedParser,
            successStatusCode: StatusCodes.Status201Created,
            successMessage: "Notification template created successfully.");
    }

    /// <summary>Admin cập nhật mẫu nội dung thông báo (NOTI-11)</summary>
    [HttpPut("admin/notifications/templates/{templateCode}")]
    [Authorize(Policy = GatewayPolicies.Admin)]
    public Task<IResult> UpdateNotificationTemplate([FromRoute] string templateCode, [FromBody] SaveTemplateRequest body)
    {
        return _dispatcher.DispatchAsync(
            HttpContext, Service, "template-create", PendingRealCall, SeedParser,
            successMessage: "Notification template updated successfully.");
    }

    /// <summary>Admin gửi thông báo hệ thống (NOTI-09)</summary>
    [HttpPost("admin/notifications/system")]
    [Authorize(Policy = GatewayPolicies.Admin)]
    public Task<IResult> SendSystemNotification([FromBody] object body)
    {
        return _dispatcher.DispatchAsync(
            HttpContext, Service, "notifications-read", PendingRealCall, SeedParser,
            successStatusCode: StatusCodes.Status200OK,
            successMessage: "System notification queued successfully.");
    }

    /// <summary>Admin/Staff tra cứu lịch sử thông báo toàn hệ thống (NOTI-09 sheet)</summary>
    [HttpGet("admin/notifications")]
    [Authorize(Policy = GatewayPolicies.Admin)]
    public Task<IResult> SearchNotificationHistory(
        [FromQuery] Guid? userId = null,
        [FromQuery] string? notificationType = null,
        [FromQuery] string? status = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
    {
        return _dispatcher.DispatchAsync(
            HttpContext, Service, "notifications-history", PendingRealCall, SeedParser,
            successMessage: "Notification history retrieved successfully.");
    }

    /// <summary>Admin tra cứu nhật ký sự kiện kỹ thuật message bus (NOTI-13)</summary>
    [HttpGet("admin/notifications/event-logs")]
    [HttpGet("admin/event-logs")]
    [Authorize(Policy = GatewayPolicies.Admin)]
    public Task<IResult> GetEventLogs(
        [FromQuery] string? sourceService = null,
        [FromQuery] string? eventType = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
    {
        return _dispatcher.DispatchAsync(
            HttpContext, Service, "event-logs-list", PendingRealCall, SeedParser,
            successMessage: "Event logs retrieved successfully.");
    }

    /// <summary>Admin xem chi tiết toàn bộ raw JSON payload của sự kiện kỹ thuật (NOTI-14)</summary>
    [HttpGet("admin/event-logs/{id:guid}")]
    [Authorize(Policy = GatewayPolicies.Admin)]
    public Task<IResult> GetEventLogDetail([FromRoute] Guid id)
    {
        return _dispatcher.DispatchAsync(
            HttpContext, Service, "event-log-detail", PendingRealCall, SeedParser,
            successMessage: "Event log detail retrieved successfully.");
    }

    /// <summary>Admin theo dõi trạng thái sức khỏe Message Broker (NOTI-16 sheet / NOTI-14)</summary>
    [HttpGet("admin/notifications/brokers/health")]
    [HttpGet("admin/health/broker")]
    [Authorize(Policy = GatewayPolicies.Admin)]
    public Task<IResult> GetBrokerHealthStatus([FromQuery] int limit = 50)
    {
        return _dispatcher.DispatchAsync(
            HttpContext, Service, "broker-health", PendingRealCall, SeedParser,
            successMessage: "Broker health status retrieved successfully.");
    }
}
