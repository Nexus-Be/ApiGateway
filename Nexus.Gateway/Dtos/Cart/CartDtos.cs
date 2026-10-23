namespace Nexus.Gateway.Dtos.Cart;

public sealed record AddCartItemRequestDto(string ProductId, int Quantity);
public sealed record UpdateCartItemRequestDto(int Quantity);

public sealed record CartItemResponseDto(string ProductId, int Quantity, string UnitPrice);
public sealed record CartResponseDto(string UserId, IReadOnlyList<CartItemResponseDto> Items, string TotalPrice);

// Compatibility aliases
public sealed record AddCartItemBody(string ProductId, int Quantity);
public sealed record UpdateCartItemBody(int Quantity);
