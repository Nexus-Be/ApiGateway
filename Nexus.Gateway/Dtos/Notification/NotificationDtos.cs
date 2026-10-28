namespace Nexus.Gateway.Dtos.Notification;

using System.Text.Json;

// ============================================================
// NOTIFICATION REQUEST DTOs — aligned with flow_mock_excel.xlsx (NOTI section)
// ============================================================

/// <summary>NOTI-01: POST /api/v1/notifications/devices — đăng ký device token</summary>
public sealed class RegisterDeviceTokenRequest
{
    public string? Platform { get; set; }  // WEB | IOS | ANDROID
    public string? Token { get; set; }
}

/// <summary>NOTI-02: DELETE /api/v1/notifications/devices — thu hồi device token</summary>
public sealed class RevokeDeviceTokenRequest
{
    public string? Token { get; set; }
}

/// <summary>NOTI-04: PUT /api/v1/notifications/preferences — cập nhật tùy chọn thông báo</summary>
public sealed class UpdateNotificationPreferencesRequest
{
    public List<NotificationPreferenceItemRequest>? Preferences { get; set; }
}

public sealed class NotificationPreferenceItemRequest
{
    public string? NotificationType { get; set; }  // ORDER_PAID | AUCTION_OUTBID | PROMOTION ...
    public string? ChannelCode { get; set; }        // EMAIL | PUSH | IN_APP
    public bool IsEnabled { get; set; }
}

/// <summary>NOTI-06: PATCH /api/v1/notifications/me/read — đánh dấu đã đọc</summary>
public sealed class MarkNotificationsReadRequest
{
    public List<string>? NotificationIds { get; set; }
    public bool MarkAll { get; set; }
}

/// <summary>NOTI-07 PUT: PUT /api/v1/admin/notifications/channels/{code} — cập nhật kênh</summary>
public sealed class UpdateNotificationChannelRequest
{
    public string? Name { get; set; }
    public string? Provider { get; set; }  // SMTP | FCM | ...
    public JsonElement? Config { get; set; }
    public bool IsEnabled { get; set; }
}

/// <summary>NOTI-08 POST: POST /api/v1/admin/notifications/templates — tạo template</summary>
public sealed class CreateNotificationTemplateRequest
{
    public string? Code { get; set; }
    public string? NotificationType { get; set; }
    public string? ChannelCode { get; set; }
    public string? SubjectTemplate { get; set; }
    public string? BodyTemplate { get; set; }
    public bool IsMandatory { get; set; }
    public string? Locale { get; set; }
    public bool IsActive { get; set; }
}

// ============================================================
// NOTIFICATION RESPONSE DTOs — aligned with flow_mock_excel.xlsx (NOTI section)
// ============================================================

/// <summary>NOTI-01: POST /api/v1/notifications/devices — 200 OK</summary>
public sealed class RegisterDeviceTokenResponseDto
{
    public string? DeviceId { get; set; }
    public string? UserId { get; set; }
    public string? Platform { get; set; }
    public bool IsActive { get; set; }
    public string? LastSeenAt { get; set; }
}

/// <summary>NOTI-03: GET /api/v1/notifications/preferences — list item</summary>
public sealed class NotificationPreferenceItemDto
{
    public string? NotificationType { get; set; }
    public string? ChannelCode { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsMandatory { get; set; }
}

/// <summary>NOTI-04: PUT /api/v1/notifications/preferences — 200 OK</summary>
public sealed class UpdatePreferencesResponseDto
{
    public int UpdatedCount { get; set; }
    public List<NotificationPreferenceItemDto>? Preferences { get; set; }
}

/// <summary>NOTI-05: GET /api/v1/notifications/me — list item (in-app bell)</summary>
public sealed class InAppNotificationItemDto
{
    public string? Id { get; set; }
    public string? NotificationType { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public object? Payload { get; set; }
    public bool IsRead { get; set; }
    public string? ReadAt { get; set; }
    public string? CreatedAt { get; set; }
}

/// <summary>NOTI-06: PATCH /api/v1/notifications/me/read — 200 OK</summary>
public sealed class MarkReadResponseDto
{
    public int UpdatedCount { get; set; }
    public int UnreadCount { get; set; }
}

/// <summary>NOTI-07 GET: GET /api/v1/admin/notifications/channels/{code} — 200 OK</summary>
public sealed class NotificationChannelResponseDto
{
    public string? Id { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Provider { get; set; }
    public JsonElement? Config { get; set; }
    public bool IsEnabled { get; set; }
    public string? UpdatedAt { get; set; }
}

/// <summary>NOTI-08 GET: GET /api/v1/admin/notifications/templates — list item</summary>
public sealed class NotificationTemplateItemDto
{
    public string? Id { get; set; }
    public string? Code { get; set; }
    public string? NotificationType { get; set; }
    public string? ChannelCode { get; set; }
    public string? SubjectTemplate { get; set; }
    public string? BodyTemplate { get; set; }
    public bool IsMandatory { get; set; }
    public string? Locale { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>NOTI-08 POST: POST /api/v1/admin/notifications/templates — 201 Created</summary>
public sealed class NotificationTemplateCreatedResponseDto
{
    public string? Id { get; set; }
    public string? Code { get; set; }
    public string? NotificationType { get; set; }
    public string? ChannelCode { get; set; }
    public bool IsMandatory { get; set; }
    public string? Locale { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>NOTI-09: GET /api/v1/admin/notifications — list item (delivery history)</summary>
public sealed class AdminNotificationItemDto
{
    public string? NotificationId { get; set; }
    public string? UserId { get; set; }
    public string? NotificationType { get; set; }
    public string? Status { get; set; }   // COMPLETED | PENDING | FAILED
    public string? CreatedAt { get; set; }
    public List<NotificationDeliveryItemDto>? Deliveries { get; set; }
}

public sealed class NotificationDeliveryItemDto
{
    public string? DeliveryId { get; set; }
    public string? ChannelCode { get; set; }
    public string? Destination { get; set; }
    public string? Status { get; set; }   // SENT | FAILED | PENDING
    public int AttemptCount { get; set; }
}

/// <summary>NOTI-13: GET /api/v1/admin/event-logs — list item</summary>
public sealed class EventLogItemDto
{
    public string? Id { get; set; }
    public string? EventId { get; set; }
    public string? EventType { get; set; }
    public string? SourceService { get; set; }
    public string? ActorId { get; set; }
    public string? OccurredAt { get; set; }
}

/// <summary>NOTI-14: GET /api/v1/admin/event-logs/{id} — 200 OK (full detail)</summary>
public sealed class EventLogDetailResponseDto
{
    public string? Id { get; set; }
    public string? EventId { get; set; }
    public string? EventType { get; set; }
    public string? SourceService { get; set; }
    public string? ActorId { get; set; }
    public object? Payload { get; set; }
    public string? OccurredAt { get; set; }
    public string? CreatedAt { get; set; }
}

/// <summary>NOTI-16: GET /api/v1/admin/health/broker — 200 OK</summary>
public sealed class BrokerHealthResponseDto
{
    public string? CurrentStatus { get; set; }   // UP | DOWN | DEGRADED
    public string? LastCheckedAt { get; set; }
    public int LatestLatencyMs { get; set; }
    public int LatestQueueDepth { get; set; }
    public List<BrokerHealthHistoryItemDto>? History { get; set; }
}

public sealed class BrokerHealthHistoryItemDto
{
    public string? Id { get; set; }
    public string? BrokerName { get; set; }
    public string? Status { get; set; }
    public int LatencyMs { get; set; }
    public int QueueDepth { get; set; }
    public string? CheckedAt { get; set; }
}

// ---------- SHARED PAGINATION ----------

public sealed class PaginationDto
{
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasPreviousPage { get; set; }
    public bool HasNextPage { get; set; }
}

// ---------- CONTROLLER-FACING ALIASES (short names) ----------

/// <summary>POST /api/v1/notifications/read — alias for MarkNotificationsReadRequest</summary>
public sealed class MarkReadRequest
{
    public List<string>? NotificationIds { get; set; }
    public bool MarkAll { get; set; }
}

/// <summary>POST /api/v1/admin/notification-templates — alias for CreateNotificationTemplateRequest</summary>
public sealed class SaveTemplateRequest
{
    public string? Code { get; set; }
    public string? NotificationType { get; set; }
    public string? BodyTemplate { get; set; }
    public string? TemplateKey { get; set; }
    public string? Name { get; set; }
    public string? Subject { get; set; }
    public string? BodyHtml { get; set; }
    public string? BodyText { get; set; }
    public string? Channel { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>PATCH /api/v1/admin/notification-channels/{id} — alias for UpdateNotificationChannelRequest</summary>
public sealed class UpdateChannelRequest
{
    public bool IsEnabled { get; set; }
    public object? Config { get; set; }
}

/// <summary>PUT /api/v1/me/notification-preferences — alias for UpdateNotificationPreferencesRequest</summary>
public sealed class UpdatePreferencesRequest
{
    public List<NotificationPreferenceItemRequest>? Preferences { get; set; }
}
