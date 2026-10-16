namespace Nexus.Gateway.Dtos.Commerce;

public sealed record CartItemResponseDto(string ProductId, int Quantity, string UnitPrice);
public sealed record CartResponseDto(string UserId, IReadOnlyList<CartItemResponseDto> Items, string TotalPrice);
public sealed record OrderResponseDto(string Id, string UserId, string Status, string TotalAmount, string CreatedAt);
