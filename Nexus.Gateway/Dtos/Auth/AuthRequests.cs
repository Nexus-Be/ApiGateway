namespace Nexus.Gateway.Dtos.Auth;

using System.Text.Json.Serialization;

// ============================================================
// AUTH REQUEST DTOs — aligned with flow_mock_excel.xlsx (rows 3–12)
// ============================================================

/// <summary>AUTH-01: POST /api/v1/auth/register</summary>
public sealed record RegisterRequestDto(
    string Email,
    string Password,
    string? Username = null,
    string? FirstName = null,
    string? LastName = null,
    string? PhoneNumber = null);

/// <summary>AUTH-02: POST /api/v1/auth/otp/verify (Exactly 2 fields as specified in flow_mock_excel.xlsx)</summary>
public sealed class VerifyOtpRequestDto
{
    public string Email { get; set; } = string.Empty;
    public string Otp { get; set; } = string.Empty;
    public string? OtpCode { get => Otp; set => Otp = value ?? string.Empty; }
}

/// <summary>AUTH-03: POST /api/v1/auth/otp/resend (Exactly 2 fields)</summary>
public sealed record ResendOtpRequestDto(
    string Email,
    string? Type = null); // "REGISTER" | "FORGOT_PASSWORD"

/// <summary>AUTH-04: POST /api/v1/auth/login (Excel specifies identifier, password, deviceInfo, ipAddress)</summary>
public sealed class LoginRequestDto
{
    public string? Identifier { get; set; }
    public string? Email { get; set; }
    public string? Username { get; set; }
    public string Password { get; set; } = string.Empty;
    public string? DeviceInfo { get; set; }
    public string? IpAddress { get; set; }

    [JsonIgnore]
    public string EffectiveIdentifier =>
        !string.IsNullOrWhiteSpace(Identifier) ? Identifier :
        !string.IsNullOrWhiteSpace(Email) ? Email :
        (Username ?? string.Empty);
}

/// <summary>AUTH-05: POST /api/v1/auth/refresh</summary>
public sealed record RefreshTokenRequestDto(
    string RefreshToken,
    string? DeviceInfo = null);

/// <summary>AUTH-06: POST /api/v1/auth/password/forgot</summary>
public sealed record ForgotPasswordRequestDto(
    string Email);

/// <summary>AUTH-07: PUT /api/v1/auth/password/reset</summary>
public sealed class ResetPasswordRequestDto
{
    public string Email { get; set; } = string.Empty;
    public string OtpCode { get; set; } = string.Empty;
    public string? Otp { get => OtpCode; set => OtpCode = value ?? string.Empty; }
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>AUTH-08: PATCH /api/v1/auth/password</summary>
public sealed record ChangePasswordRequestDto(
    string OldPassword,
    string NewPassword,
    string ConfirmPassword);

/// <summary>AUTH-09: POST /api/v1/auth/logout</summary>
public sealed record LogoutRequestDto(
    string? RefreshToken = null);
