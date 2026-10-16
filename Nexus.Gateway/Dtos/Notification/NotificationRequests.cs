namespace Nexus.Gateway.Dtos.Notification;

public sealed record UpdatePreferencesRequestDto(bool EmailEnabled, bool PushEnabled, bool OrderUpdates, bool AuctionAlerts);

// Compatibility aliases
public sealed record UpdatePreferencesBody(bool EmailEnabled, bool PushEnabled, bool OrderUpdates, bool AuctionAlerts);
