namespace Nexus.Gateway.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.Gateway.Common;
using Nexus.Gateway.Dispatching;
using Nexus.Gateway.Dtos.Auth;
using Nexus.Gateway.Responses;
using Nexus.Gateway.Security;
using ShoppingCart.Contracts.V1;

[ApiController]
[Route("api/v1/auth")]
[BackendTag("users")]
[EnableRateLimiting("api")]
public sealed class AuthController : ControllerBase
{
    private readonly IGatewayDispatcher _dispatcher;
    private readonly UsersApi.UsersApiClient _client;

    public AuthController(IGatewayDispatcher dispatcher, UsersApi.UsersApiClient client)
    {
        _dispatcher = dispatcher;
        _client = client;
    }

    [HttpPost("register")]
    [AllowAnonymous]
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
            "users",
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
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<UserProfile>), StatusCodes.Status200OK)]
    public Task<IResult> VerifyOtp([FromBody] VerifyOtpRequestDto body)
    {
        if (string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.Otp))
            throw new GatewayInputException("Email and OTP code are required.");

        return _dispatcher.DispatchAsync(
            HttpContext,
            "users",
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
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<OtpResponse>), StatusCodes.Status200OK)]
    public Task<IResult> ResendOtp([FromBody] ResendOtpRequestDto body)
    {
        if (string.IsNullOrWhiteSpace(body.Email))
            throw new GatewayInputException("Email address is required.");

        return _dispatcher.DispatchAsync(
            HttpContext,
            "users",
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
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    public Task<IResult> Login([FromBody] LoginRequestDto body)
    {
        Response.Headers.CacheControl = "no-store";
        if (string.IsNullOrWhiteSpace(body.Username) || string.IsNullOrEmpty(body.Password))
            throw new GatewayInputException("Username and password are required.");

        return _dispatcher.DispatchAsync(
            HttpContext,
            "users",
            "login",
            o => _client.LoginAsync(new LoginRequest
            {
                Username = body.Username,
                Password = body.Password
            }, o),
            LoginResponse.Parser,
            successMessage: "Login successful."
        );
    }
}
