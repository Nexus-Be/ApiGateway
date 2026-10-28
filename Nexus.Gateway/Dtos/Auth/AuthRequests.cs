namespace Nexus.Gateway.Dtos.Auth;

using System.Text.Json.Serialization;

// ============================================================
// AUTH REQUEST DTOs — aligned with flow_mock_excel.xlsx
// ============================================================

/// <summary>AUTH-01: POST /api/v1/auth/register</summary>
public sealed record RegisterRequestDto(
    string? Username,
    string Email,
    string Password,
    string? DisplayName = null,
    string? FirstName = null,
    string? LastName = null,
    string? PhoneNumber = null);

/// <summary>AUTH-02: POST /api/v1/auth/otp/verify</summary>
public sealed record VerifyOtpRequestDto(
    string Email,
    string? Otp = null,
    [property: JsonPropertyName("otp_code")] string? OtpCode = null)
{
    [JsonIgnore]
    public string? ResolvedOtp => !string.IsNullOrWhiteSpace(Otp) ? Otp : OtpCode;
}

/// <summary>AUTH-03: POST /api/v1/auth/otp/resend</summary>
public sealed record ResendOtpRequestDto(
    string Email,
    string? Type = null); // "REGISTER" | "FORGOT_PASSWORD"

/// <summary>AUTH-04: POST /api/v1/auth/login</summary>
public sealed record LoginRequestDto(
    string? Username,
    string Password,
    string? Identifier = null,
    string? DeviceInfo = null,
    string? IpAddress = null)
{
    [JsonIgnore]
    public string EffectiveUsername => !string.IsNullOrWhiteSpace(Username) ? Username : (Identifier ?? string.Empty);
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

// ---------- CONTROLLER-FACING ALIASES (short names, without "Request" suffix) ----------
// AuthController uses these shorter names without the "Request" suffix:

/// <summary>AUTH-05 controller name alias: POST /api/v1/auth/refresh</summary>
public sealed record RefreshTokenDto(
    string? RefreshToken = null,
    string? DeviceInfo = null);

/// <summary>AUTH-06 controller name alias: POST /api/v1/auth/password/forgot</summary>
public sealed record ForgotPasswordDto(
    string? Email = null);

/// <summary>AUTH-07 controller name alias: PUT /api/v1/auth/password/reset</summary>
public sealed record ResetPasswordDto(
    string? Email = null,
    string? OtpCode = null,
    string? NewPassword = null,
    string? ConfirmPassword = null);

/// <summary>AUTH-08 controller name alias: PATCH /api/v1/auth/password</summary>
public sealed record ChangePasswordDto(
    string? OldPassword = null,
    string? NewPassword = null,
    string? ConfirmPassword = null);

/// <summary>AUTH-09 controller name alias: POST /api/v1/auth/logout</summary>
public sealed record LogoutDto(
    string? RefreshToken = null);
