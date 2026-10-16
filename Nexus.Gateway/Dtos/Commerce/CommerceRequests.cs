namespace Nexus.Gateway.Dtos.Commerce;

public sealed record CheckoutLineDto(string ProductId, int Quantity);
public sealed record CheckoutRequestDto(string? CartId, IReadOnlyList<CheckoutLineDto>? Items);
public sealed record AddCartItemRequestDto(string ProductId, int Quantity);
public sealed record UpdateCartItemRequestDto(int Quantity);

// Compatibility aliases
public sealed record CheckoutLine(string ProductId, int Quantity);
public sealed record CheckoutBody(string? CartId, IReadOnlyList<CheckoutLine>? Items);
public sealed record AddCartItemBody(string ProductId, int Quantity);
public sealed record UpdateCartItemBody(int Quantity);
