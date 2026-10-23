namespace Nexus.Gateway.Dtos.Auth;

public sealed record RegisterRequestDto(
    string? Username,
    string Email,
    string Password,
    string? DisplayName = null,
    string? FirstName = null,
    string? LastName = null,
    string? PhoneNumber = null);

public sealed record LoginRequestDto(
    string Username,
    string Password);

public sealed record VerifyOtpRequestDto(
    string Email,
    string Otp);

public sealed record ResendOtpRequestDto(
    string Email);

// Compatibility aliases
public sealed record RegisterBody(
    string? Username,
    string Email,
    string Password,
    string? DisplayName = null,
    string? FirstName = null,
    string? LastName = null,
    string? PhoneNumber = null);

public sealed record LoginBody(
    string Username,
    string Password);
