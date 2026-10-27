namespace Nexus.Gateway.Controllers;

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.Gateway.Common;
using Nexus.Gateway.Dispatching;
using Nexus.Gateway.Dtos.Auth;
using Nexus.Gateway.Dtos.User;
using Nexus.Gateway.Responses;
using Nexus.Gateway.Security;
using ShoppingCart.Contracts.V1;

[ApiController]
[Route("api/v1/auth")]
[BackendTag("users")]
[EnableRateLimiting("api")]
public sealed class AuthController : GatewayBaseController
{
    private const string Service = "users";
    private readonly UsersApi.UsersApiClient _client;
    private readonly UserApi.UserApiClient _userClient;

    public AuthController(
        IGatewayDispatcher dispatcher,
        UsersApi.UsersApiClient client,
        UserApi.UserApiClient userClient) : base(dispatcher)
    {
        _client = client;
        _userClient = userClient;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-strict")]
    [ProducesResponseType(typeof(ApiResponse<UserProfile>), StatusCodes.Status200OK)]
    public Task<IResult> Register([FromBody] RegisterRequestDto body)
    {
        var username = body.Username ?? (body.Email?.Contains('@') == true ? body.Email.Split('@')[0] : null);
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(body.Email) || !body.Email.Contains('@') || (body.Password?.Length ?? 0) < 8)
            throw new GatewayInputException("Username, valid email, and password with at least 8 characters are required.");

        var displayName = body.DisplayName ?? $"{body.FirstName} {body.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(displayName)) displayName = username;

        return ForwardAsync(
            Service,
            "/api/v1/auth/register",
            HttpMethod.Post,
            grpcCall: o => _client.RegisterAsync(new RegisterRequest
            {
                Username = username,
                Email = body.Email.Trim().ToLowerInvariant(),
                Password = body.Password,
                DisplayName = displayName,
                FirstName = body.FirstName ?? "",
                LastName = body.LastName ?? "",
                PhoneNumber = body.PhoneNumber ?? ""
            }, o),
            parser: UserProfile.Parser,
            body: body,
            mockSeed: "register",
            successMessage: "User registered successfully."
        );
    }

    [HttpPost("verify-otp")]
    [HttpPost("otp/verify")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-strict")]
    [ProducesResponseType(typeof(ApiResponse<UserProfile>), StatusCodes.Status200OK)]
    public Task<IResult> VerifyOtp([FromBody] VerifyOtpRequestDto body)
    {
        if (string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.ResolvedOtp))
            throw new GatewayInputException("Email and OTP code are required.");

        return ForwardAsync(
            Service,
            "/api/v1/auth/otp/verify",
            HttpMethod.Post,
            grpcCall: o => _client.VerifyOtpAsync(new VerifyOtpRequest
            {
                Email = body.Email.Trim().ToLowerInvariant(),
                Otp = body.ResolvedOtp!.Trim()
            }, o),
            parser: UserProfile.Parser,
            body: body,
            mockSeed: "verify-otp",
            successMessage: "Account verified successfully."
        );
    }

    [HttpPost("resend-otp")]
    [HttpPost("otp/resend")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-strict")]
    [ProducesResponseType(typeof(ApiResponse<OtpResponse>), StatusCodes.Status200OK)]
    public Task<IResult> ResendOtp([FromBody] ResendOtpRequestDto body)
    {
        if (string.IsNullOrWhiteSpace(body.Email))
            throw new GatewayInputException("Email address is required.");

        return ForwardAsync(
            Service,
            "/api/v1/auth/otp/resend",
            HttpMethod.Post,
            grpcCall: o => _client.ResendOtpAsync(new ResendOtpRequest
            {
                Email = body.Email.Trim().ToLowerInvariant()
            }, o),
            parser: OtpResponse.Parser,
            body: body,
            mockSeed: "resend-otp",
            successMessage: "Verification code has been resent to your email."
        );
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    public Task<IResult> Login([FromBody] LoginRequestDto body)
    {
        Response.Headers.CacheControl = "no-store";
        if (string.IsNullOrWhiteSpace(body.Username) || string.IsNullOrEmpty(body.Password))
            throw new GatewayInputException("Username and password are required.");

        return ForwardAsync(
            Service,
            "/api/v1/auth/login",
            HttpMethod.Post,
            grpcCall: o => _client.LoginAsync(new LoginRequest
            {
                Username = body.Username.Trim(),
                Password = body.Password
            }, o),
            parser: LoginResponse.Parser,
            body: body,
            mockSeed: "login",
            successMessage: "Login successful."
        );
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    [ProducesResponseType(typeof(ApiResponse<RefreshTokenResponse>), StatusCodes.Status200OK)]
    public Task<IResult> Refresh([FromBody] RefreshTokenDto body)
    {
        Response.Headers.CacheControl = "no-store";
        var refreshToken = UserValidation.EnsureRequired(body.RefreshToken, "RefreshToken", "Refresh token");

        return ForwardAsync(
            Service,
            "/api/v1/auth/refresh",
            HttpMethod.Post,
            grpcCall: o => _userClient.RefreshTokenAsync(new RefreshTokenRequest
            {
                RefreshToken = refreshToken,
                DeviceInfo = body.DeviceInfo ?? string.Empty
            }, o),
            parser: RefreshTokenResponse.Parser,
            body: body,
            mockSeed: "refresh",
            successMessage: "Token refreshed successfully."
        );
    }

    [HttpPost("password/forgot")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-strict")]
    [ProducesResponseType(typeof(ApiResponse<ForgotPasswordResponse>), StatusCodes.Status200OK)]
    public Task<IResult> ForgotPassword([FromBody] ForgotPasswordDto body)
    {
        var email = UserValidation.EnsureEmail(body.Email);

        return ForwardAsync(
            Service,
            "/api/v1/auth/password/forgot",
            HttpMethod.Post,
            grpcCall: o => _userClient.ForgotPasswordAsync(new ForgotPasswordRequest
            {
                Email = email
            }, o),
            parser: ForgotPasswordResponse.Parser,
            body: body,
            mockSeed: "password-forgot",
            successMessage: "Password reset instructions have been sent to your email."
        );
    }

    [HttpPost("password/reset")]
    [HttpPut("password/reset")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-strict")]
    [ProducesResponseType(typeof(ApiResponse<ResetPasswordResponse>), StatusCodes.Status200OK)]
    public Task<IResult> ResetPassword([FromBody] ResetPasswordDto body)
    {
        var email = UserValidation.EnsureEmail(body.Email);
        var otp = UserValidation.EnsureOtp(body.OtpCode);
        var newPassword = UserValidation.EnsurePassword(body.NewPassword, "NewPassword");
        var confirmPassword = UserValidation.EnsureRequired(body.ConfirmPassword, "ConfirmPassword", "Confirm password");

        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
        {
            throw new GatewayInputException("Password confirmation does not match.", "ConfirmPassword", UserValidation.ErrorCode);
        }

        return ForwardAsync(
            Service,
            "/api/v1/auth/password/reset",
            HttpMethod.Put,
            grpcCall: o => _userClient.ResetPasswordAsync(new ResetPasswordRequest
            {
                Email = email,
                OtpCode = otp,
                NewPassword = newPassword,
                ConfirmPassword = confirmPassword
            }, o),
            parser: ResetPasswordResponse.Parser,
            body: body,
            mockSeed: "password-reset",
            successMessage: "Password has been reset successfully."
        );
    }

    [HttpPatch("password")]
    [HttpPost("password/change")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<ChangePasswordResponse>), StatusCodes.Status200OK)]
    public Task<IResult> ChangePassword([FromBody] ChangePasswordDto body)
    {
        var oldPassword = UserValidation.EnsureRequired(body.OldPassword, "OldPassword", "Old password");
        var newPassword = UserValidation.EnsurePassword(body.NewPassword, "NewPassword");
        var confirmPassword = UserValidation.EnsureRequired(body.ConfirmPassword, "ConfirmPassword", "Confirm password");

        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
        {
            throw new GatewayInputException("Password confirmation does not match.", "ConfirmPassword", UserValidation.ErrorCode);
        }

        return ForwardAsync(
            Service,
            "/api/v1/auth/password",
            HttpMethod.Patch,
            grpcCall: o => _userClient.ChangePasswordAsync(new ChangePasswordRequest
            {
                OldPassword = oldPassword,
                NewPassword = newPassword,
                ConfirmPassword = confirmPassword,
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            parser: ChangePasswordResponse.Parser,
            body: body,
            mockSeed: "password-change",
            successMessage: "Password changed successfully."
        );
    }

    [HttpPost("logout")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<LogoutResponse>), StatusCodes.Status200OK)]
    public Task<IResult> Logout([FromBody] LogoutDto? body)
    {
        return ForwardAsync(
            Service,
            "/api/v1/auth/logout",
            HttpMethod.Post,
            grpcCall: o => _userClient.LogoutAsync(new LogoutRequest
            {
                RefreshToken = body?.RefreshToken ?? string.Empty,
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            parser: LogoutResponse.Parser,
            body: body,
            mockSeed: "logout",
            successMessage: "Logged out successfully."
        );
    }

    [HttpPost("logout-all")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<LogoutAllResponse>), StatusCodes.Status200OK)]
    public Task<IResult> LogoutAll()
    {
        return ForwardAsync(
            Service,
            "/api/v1/auth/logout-all",
            HttpMethod.Post,
            grpcCall: o => _userClient.LogoutAllAsync(new LogoutAllRequest
            {
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            parser: LogoutAllResponse.Parser,
            mockSeed: "logout-all",
            successMessage: "All sessions have been revoked."
        );
    }
}
