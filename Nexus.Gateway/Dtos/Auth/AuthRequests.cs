namespace Nexus.Gateway.Dtos.Auth;

public sealed record RegisterRequestDto(string Username, string Email, string Password, string? DisplayName);
public sealed record LoginRequestDto(string Username, string Password);

// Compatibility aliases
public sealed record RegisterBody(string Username, string Email, string Password, string? DisplayName);
public sealed record LoginBody(string Username, string Password);
