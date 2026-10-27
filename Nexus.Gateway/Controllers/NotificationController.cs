namespace Nexus.Gateway.Controllers;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
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
public sealed class NotificationController : GatewayBaseController
{
    private const string Service = "notification";

    public NotificationController(IGatewayDispatcher dispatcher) : base(dispatcher)
    {
    }

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

        return ForwardAsync(
            Service,
            "/api/v1/notifications/devices",
            HttpMethod.Post,
            body: body,
            mockSeed: "device-register",
            successMessage: "Device token registered successfully."
        );
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

        return ForwardNoContentAsync(
            Service,
            "/api/v1/notifications/devices",
            HttpMethod.Delete,
            body: body,
            mockSeed: "device-revoke"
        );
    }

    /// <summary>Lấy danh sách cấu hình bật/tắt nhận thông báo của người dùng (NOTI-03)</summary>
    [HttpGet("notifications/preferences")]
    [Authorize(Policy = GatewayPolicies.Authenticated)]
    public Task<IResult> GetMyPreferences()
    {
        return ForwardAsync(
            Service,
            "/api/v1/notifications/preferences",
            HttpMethod.Get,
            mockSeed: "preferences-get",
            successMessage: "Notification preferences retrieved successfully."
        );
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

        return ForwardAsync(
            Service,
            "/api/v1/notifications/preferences",
            HttpMethod.Put,
            body: body,
            mockSeed: "preferences-update",
            successMessage: "Notification preferences updated successfully."
        );
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

        var query = new Dictionary<string, string?>();
        if (isRead.HasValue) query["isRead"] = isRead.Value.ToString().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(notificationType)) query["notificationType"] = notificationType;
        query["pageIndex"] = pageIndex.ToString();
        query["pageSize"] = pageSize.ToString();

        return ForwardAsync(
            Service,
            "/api/v1/notifications/me",
            HttpMethod.Get,
            mockSeed: "notifications-me",
            successMessage: "Notifications retrieved successfully.",
            queryParams: query
        );
    }

    /// <summary>Đánh dấu thông báo đã đọc qua batch body (NOTI-06 / NOTI-08)</summary>
    [HttpPatch("notifications/me/read")]
    [Authorize(Policy = GatewayPolicies.Authenticated)]
    public Task<IResult> MarkNotificationsAsRead([FromBody] MarkReadRequest body)
    {
        return ForwardAsync(
            Service,
            "/api/v1/notifications/me/read",
            HttpMethod.Patch,
            body: body,
            mockSeed: "notifications-read",
            successMessage: "Notifications marked as read successfully."
        );
    }

    /// <summary>Admin/Staff xem cấu hình kênh gửi thông báo (NOTI-07 sheet)</summary>
    [HttpGet("admin/notifications/channels/{code}")]
    [Authorize(Policy = GatewayPolicies.StaffOrAdmin)]
    public Task<IResult> GetNotificationChannel([FromRoute] string code)
    {
        return ForwardAsync(
            Service,
            $"/api/v1/admin/notifications/channels/{code}",
            HttpMethod.Get,
            mockSeed: "channel-detail",
            successMessage: "Notification channel retrieved successfully."
        );
    }

    /// <summary>Admin/Staff cập nhật cấu hình kênh gửi thông báo (NOTI-07 sheet)</summary>
    [HttpPut("admin/notifications/channels/{code}")]
    [Authorize(Policy = GatewayPolicies.StaffOrAdmin)]
    public Task<IResult> UpdateNotificationChannel([FromRoute] string code, [FromBody] UpdateChannelRequest body)
    {
        return ForwardAsync(
            Service,
            $"/api/v1/admin/notifications/channels/{code}",
            HttpMethod.Put,
            body: body,
            mockSeed: "channel-detail",
            successMessage: "Notification channel updated successfully."
        );
    }

    /// <summary>Admin/Staff xem danh sách mẫu thông báo (NOTI-08 sheet / NOTI-12)</summary>
    [HttpGet("admin/notifications/templates")]
    [Authorize(Policy = GatewayPolicies.StaffOrAdmin)]
    public Task<IResult> GetNotificationTemplates(
        [FromQuery] string? channelCode = null,
        [FromQuery] string? notificationType = null)
    {
        var query = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(channelCode)) query["channelCode"] = channelCode;
        if (!string.IsNullOrWhiteSpace(notificationType)) query["notificationType"] = notificationType;

        return ForwardAsync(
            Service,
            "/api/v1/admin/notifications/templates",
            HttpMethod.Get,
            mockSeed: "templates-get",
            successMessage: "Notification templates retrieved successfully.",
            queryParams: query
        );
    }

    /// <summary>Admin/Staff tạo mẫu nội dung thông báo mới (NOTI-08 sheet / NOTI-10)</summary>
    [HttpPost("admin/notifications/templates")]
    [Authorize(Policy = GatewayPolicies.StaffOrAdmin)]
    public Task<IResult> CreateNotificationTemplate([FromBody] SaveTemplateRequest body)
    {
        if (string.IsNullOrWhiteSpace(body.Code) ||
            string.IsNullOrWhiteSpace(body.NotificationType) ||
            string.IsNullOrWhiteSpace(body.BodyTemplate))
        {
            throw new GatewayInputException("Code, NotificationType and BodyTemplate are required.", "Template");
        }

        return ForwardAsync(
            Service,
            "/api/v1/admin/notifications/templates",
            HttpMethod.Post,
            body: body,
            mockSeed: "template-create",
            successStatusCode: StatusCodes.Status201Created,
            successMessage: "Notification template created successfully."
        );
    }

    /// <summary>Admin/Staff tra cứu lịch sử thông báo toàn hệ thống (NOTI-09 sheet)</summary>
    [HttpGet("admin/notifications")]
    [Authorize(Policy = GatewayPolicies.StaffOrAdmin)]
    public Task<IResult> SearchNotificationHistory(
        [FromQuery] Guid? userId = null,
        [FromQuery] string? notificationType = null,
        [FromQuery] string? status = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new Dictionary<string, string?>();
        if (userId.HasValue) query["userId"] = userId.Value.ToString();
        if (!string.IsNullOrWhiteSpace(notificationType)) query["notificationType"] = notificationType;
        if (!string.IsNullOrWhiteSpace(status)) query["status"] = status;
        query["pageIndex"] = pageIndex.ToString();
        query["pageSize"] = pageSize.ToString();

        return ForwardAsync(
            Service,
            "/api/v1/admin/notifications",
            HttpMethod.Get,
            mockSeed: "notifications-history",
            successMessage: "Notification history retrieved successfully.",
            queryParams: query
        );
    }

    /// <summary>Admin/Staff tra cứu nhật ký sự kiện kỹ thuật message bus (NOTI-13)</summary>
    [HttpGet("admin/event-logs")]
    [Authorize(Policy = GatewayPolicies.StaffOrAdmin)]
    public Task<IResult> GetEventLogs(
        [FromQuery] string? sourceService = null,
        [FromQuery] string? eventType = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(sourceService)) query["sourceService"] = sourceService;
        if (!string.IsNullOrWhiteSpace(eventType)) query["eventType"] = eventType;
        query["pageIndex"] = pageIndex.ToString();
        query["pageSize"] = pageSize.ToString();

        return ForwardAsync(
            Service,
            "/api/v1/admin/event-logs",
            HttpMethod.Get,
            mockSeed: "event-logs-list",
            successMessage: "Event logs retrieved successfully.",
            queryParams: query
        );
    }

    /// <summary>Admin/Staff xem chi tiết toàn bộ raw JSON payload của sự kiện kỹ thuật (NOTI-14)</summary>
    [HttpGet("admin/event-logs/{id:guid}")]
    [Authorize(Policy = GatewayPolicies.StaffOrAdmin)]
    public Task<IResult> GetEventLogDetail([FromRoute] Guid id)
    {
        return ForwardAsync(
            Service,
            $"/api/v1/admin/event-logs/{id}",
            HttpMethod.Get,
            mockSeed: "event-log-detail",
            successMessage: "Event log detail retrieved successfully."
        );
    }

    /// <summary>Admin/Staff theo dõi trạng thái sức khỏe Message Broker (NOTI-16 sheet / NOTI-14)</summary>
    [HttpGet("admin/health/broker")]
    [Authorize(Policy = GatewayPolicies.StaffOrAdmin)]
    public Task<IResult> GetBrokerHealthStatus([FromQuery] int limit = 50)
    {
        var query = new Dictionary<string, string?>
        {
            ["limit"] = limit.ToString()
        };

        return ForwardAsync(
            Service,
            "/api/v1/admin/health/broker",
            HttpMethod.Get,
            mockSeed: "broker-health",
            successMessage: "Broker health status retrieved successfully.",
            queryParams: query
        );
    }
}
