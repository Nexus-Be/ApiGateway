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
public sealed record VerifyOtpRequestDto(
    string Email,
    string Otp);

/// <summary>AUTH-03: POST /api/v1/auth/otp/resend (Exactly 2 fields)</summary>
public sealed record ResendOtpRequestDto(
    string Email,
    string? Type = null); // "REGISTER" | "FORGOT_PASSWORD"

/// <summary>AUTH-04: POST /api/v1/auth/login (Excel specifies identifier, password, deviceInfo, ipAddress)</summary>
public sealed record LoginRequestDto(
    string? Identifier = null,
    string Password = "",
    string? DeviceInfo = null,
    string? IpAddress = null,
    string? Username = null)
{
    [JsonIgnore]
    public string EffectiveIdentifier => !string.IsNullOrWhiteSpace(Identifier) ? Identifier : (Username ?? string.Empty);
}

/// <summary>AUTH-05: POST /api/v1/auth/refresh</summary>
public sealed record RefreshTokenRequestDto(
    string RefreshToken,
    string? DeviceInfo = null);

/// <summary>AUTH-06: POST /api/v1/auth/password/forgot</summary>
public sealed record ForgotPasswordRequestDto(
    string Email);

/// <summary>AUTH-07: PUT /api/v1/auth/password/reset</summary>
public sealed record ResetPasswordRequestDto(
    string Email,
    string OtpCode,
    string NewPassword,
    string ConfirmPassword);

/// <summary>AUTH-08: PATCH /api/v1/auth/password</summary>
public sealed record ChangePasswordRequestDto(
    string OldPassword,
    string NewPassword,
    string ConfirmPassword);

/// <summary>AUTH-09: POST /api/v1/auth/logout</summary>
public sealed record LogoutRequestDto(
    string? RefreshToken = null);
