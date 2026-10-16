namespace Nexus.Gateway.Dtos.Notification;

public sealed record NotificationResponseDto(string Id, string UserId, string Title, string Content, bool IsRead, string CreatedAt);
public sealed record NotificationPreferencesResponseDto(bool EmailEnabled, bool PushEnabled, bool OrderUpdates, bool AuctionAlerts);
