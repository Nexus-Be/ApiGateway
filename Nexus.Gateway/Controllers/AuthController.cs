namespace Nexus.Gateway.Controllers;

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
public sealed class AuthController : ControllerBase
{
    private const string Service = "users";
    private readonly IGatewayDispatcher _dispatcher;
    private readonly UsersApi.UsersApiClient _client;
    private readonly UserApi.UserApiClient _userClient;

    public AuthController(
        IGatewayDispatcher dispatcher,
        UsersApi.UsersApiClient client,
        UserApi.UserApiClient userClient)
    {
        _dispatcher = dispatcher;
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

        return _dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "register",
            o => _client.RegisterAsync(new RegisterRequest
            {
                Username = username,
                Email = body.Email.Trim().ToLowerInvariant(),
                Password = body.Password,
                DisplayName = displayName,
                FirstName = body.FirstName ?? "",
                LastName = body.LastName ?? "",
                PhoneNumber = body.PhoneNumber ?? ""
            }, o),
            UserProfile.Parser,
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
        if (string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.Otp))
            throw new GatewayInputException("Email and OTP code are required.");

        return _dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "verify-otp",
            o => _client.VerifyOtpAsync(new VerifyOtpRequest
            {
                Email = body.Email.Trim().ToLowerInvariant(),
                Otp = body.Otp.Trim()
            }, o),
            UserProfile.Parser,
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

        return _dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "resend-otp",
            o => _client.ResendOtpAsync(new ResendOtpRequest
            {
                Email = body.Email.Trim().ToLowerInvariant()
            }, o),
            OtpResponse.Parser,
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

        return _dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "login",
            o => _client.LoginAsync(new LoginRequest
            {
                Username = body.Username.Trim(),
                Password = body.Password
            }, o),
            LoginResponse.Parser,
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

        return _dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "refresh",
            o => _userClient.RefreshTokenAsync(new RefreshTokenRequest
            {
                RefreshToken = refreshToken,
                DeviceInfo = body.DeviceInfo ?? string.Empty
            }, o),
            RefreshTokenResponse.Parser,
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

        return _dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "password-forgot",
            o => _userClient.ForgotPasswordAsync(new ForgotPasswordRequest
            {
                Email = email
            }, o),
            ForgotPasswordResponse.Parser,
            successMessage: "Password reset instructions have been sent to your email."
        );
    }

    [HttpPost("password/reset")]
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

        return _dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "password-reset",
            o => _userClient.ResetPasswordAsync(new ResetPasswordRequest
            {
                Email = email,
                OtpCode = otp,
                NewPassword = newPassword,
                ConfirmPassword = confirmPassword
            }, o),
            ResetPasswordResponse.Parser,
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

        return _dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "password-change",
            o => _userClient.ChangePasswordAsync(new ChangePasswordRequest
            {
                OldPassword = oldPassword,
                NewPassword = newPassword,
                ConfirmPassword = confirmPassword,
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            ChangePasswordResponse.Parser,
            successMessage: "Password changed successfully."
        );
    }

    [HttpPost("logout")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<LogoutResponse>), StatusCodes.Status200OK)]
    public Task<IResult> Logout([FromBody] LogoutDto? body)
    {
        return _dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "logout",
            o => _userClient.LogoutAsync(new LogoutRequest
            {
                RefreshToken = body?.RefreshToken ?? string.Empty,
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            LogoutResponse.Parser,
            successMessage: "Logged out successfully."
        );
    }

    [HttpPost("logout-all")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<LogoutAllResponse>), StatusCodes.Status200OK)]
    public Task<IResult> LogoutAll()
    {
        return _dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "logout-all",
            o => _userClient.LogoutAllAsync(new LogoutAllRequest
            {
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            LogoutAllResponse.Parser,
            successMessage: "All sessions have been revoked."
        );
    }
}
