namespace Nexus.Gateway.Dtos.Auth;

// ============================================================
// AUTH RESPONSE DTOs — aligned with flow_mock_excel.xlsx
// ============================================================

/// <summary>AUTH-01 response data: POST /api/v1/auth/register — 201 Created</summary>
public sealed record RegisterResponseDto(
    string UserId,
    string Email,
    int OtpExpiresIn); // 300 seconds

/// <summary>AUTH-02 response data: POST /api/v1/auth/otp/verify — 200 OK</summary>
public sealed record VerifyOtpResponseDto(
    bool IsVerified,
    string Email);

/// <summary>AUTH-03 response data: POST /api/v1/auth/otp/resend — 200 OK</summary>
public sealed record ResendOtpResponseDto(
    string Email,
    int ResendCooldown); // 60 seconds

/// <summary>AUTH-04 response data: POST /api/v1/auth/login — 201 Created</summary>
public sealed record LoginResponseDto(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    LoginUserDto User);

public sealed record LoginUserDto(
    string Id,
    string Email,
    string? Username,
    string FirstName,
    string LastName,
    string[] Roles,
    string[] Permissions);

/// <summary>AUTH-05 response data: POST /api/v1/auth/refresh — 200 OK</summary>
public sealed record RefreshTokenResponseDto(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn);

/// <summary>AUTH-06 response data: POST /api/v1/auth/password/forgot — 200 OK</summary>
public sealed record ForgotPasswordResponseDto(
    string Email);

/// <summary>AUTH-07 response data: PUT /api/v1/auth/password/reset — 200 OK</summary>
public sealed record ResetPasswordResponseDto(
    bool Success);

/// <summary>AUTH-08 response data: PATCH /api/v1/auth/password — 200 OK</summary>
public sealed record ChangePasswordResponseDto(
    bool Success);

/// <summary>AUTH-09 response data: POST /api/v1/auth/logout — 200 OK</summary>
public sealed record LogoutResponseDto(
    bool Success);

/// <summary>AUTH-USER-05 response data: POST /api/v1/auth/logout-all — 200 OK</summary>
public sealed record LogoutAllResponseDto(
    int CountRevoked);
