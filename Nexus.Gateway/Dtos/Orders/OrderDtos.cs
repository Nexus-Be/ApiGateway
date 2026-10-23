namespace Nexus.Gateway.Dtos.Orders;

public sealed record CheckoutLineDto(string ProductId, int Quantity);
public sealed record CheckoutRequestDto(string? CartId, IReadOnlyList<CheckoutLineDto>? Items);

public sealed record OrderResponseDto(string Id, string UserId, string Status, string TotalAmount, string CreatedAt);

// Compatibility aliases
public sealed record CheckoutLine(string ProductId, int Quantity);
public sealed record CheckoutBody(string? CartId, IReadOnlyList<CheckoutLine>? Items);
