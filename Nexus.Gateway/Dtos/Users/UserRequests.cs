namespace Nexus.Gateway.Dtos.Users;

public sealed record UpdateProfileRequestDto(string? DisplayName, string? Email);
public sealed record SellerUpgradeRequestDto(string ShopName, string? Description, string? ContactEmail, string? ContactPhone, string? Address);
public sealed record CreateUserRequestDto(string Username, string Email, string Password, string? DisplayName, string[]? Roles);
public sealed record AssignRolesRequestDto(string[] Roles);
public sealed record ApproveSellerRequestDto(bool Approve, string? Reason);

// Compatibility aliases
public sealed record UpdateProfileBody(string? DisplayName, string? Email);
public sealed record SellerUpgradeBody(string ShopName, string? Description, string? ContactEmail, string? ContactPhone, string? Address);
public sealed record CreateUserBody(string Username, string Email, string Password, string? DisplayName, string[]? Roles);
public sealed record AssignRolesBody(string[] Roles);
public sealed record ApproveSellerBody(bool Approve, string? Reason);
