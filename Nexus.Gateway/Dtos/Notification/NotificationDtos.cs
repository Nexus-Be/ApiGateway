namespace Nexus.Gateway.Dtos.Notification;

using System.Text.Json.Serialization;

public sealed record RegisterDeviceTokenRequest(
    [property: JsonPropertyName("platform")] string Platform,
    [property: JsonPropertyName("token")] string Token);

public sealed record RevokeDeviceTokenRequest(
    [property: JsonPropertyName("token")] string Token);

public sealed record PreferenceItemDto(
    [property: JsonPropertyName("notificationType")] string NotificationType,
    [property: JsonPropertyName("channelCode")] string ChannelCode,
    [property: JsonPropertyName("isEnabled")] bool IsEnabled);

public sealed record UpdatePreferencesRequest(
    [property: JsonPropertyName("preferences")] IReadOnlyList<PreferenceItemDto> Preferences);

public sealed record MarkReadRequest(
    [property: JsonPropertyName("notificationIds")] IReadOnlyList<string>? NotificationIds,
    [property: JsonPropertyName("markAll")] bool MarkAll = false);

public sealed record UpdateChannelRequest(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("config")] object? Config,
    [property: JsonPropertyName("isEnabled")] bool IsEnabled);

public sealed record SaveTemplateRequest(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("notificationType")] string NotificationType,
    [property: JsonPropertyName("channelCode")] string ChannelCode,
    [property: JsonPropertyName("subjectTemplate")] string? SubjectTemplate,
    [property: JsonPropertyName("bodyTemplate")] string BodyTemplate,
    [property: JsonPropertyName("isMandatory")] bool IsMandatory,
    [property: JsonPropertyName("locale")] string Locale = "vi-VN",
    [property: JsonPropertyName("isActive")] bool IsActive = true);
