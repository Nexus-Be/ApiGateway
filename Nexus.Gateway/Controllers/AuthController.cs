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
[Route("gateway/api/v1/auth")]
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
        if (string.IsNullOrWhiteSpace(body.Username) || !body.Email.Contains('@') || (body.Password?.Length ?? 0) < 8)
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "username, email hợp lệ và password >= 8 ký tự là bắt buộc"));

        return _dispatcher.DispatchAsync(
            HttpContext,
            "users",
            "register",
            o => _client.RegisterAsync(new RegisterRequest
            {
                Username = body.Username,
                Email = body.Email,
                Password = body.Password,
                DisplayName = body.DisplayName ?? body.Username
            }, o),
            UserProfile.Parser,
            successMessage: "User registered successfully."
        );
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    public Task<IResult> Login([FromBody] LoginRequestDto body)
    {
        Response.Headers.CacheControl = "no-store";
        if (string.IsNullOrWhiteSpace(body.Username) || string.IsNullOrEmpty(body.Password))
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "username và password là bắt buộc"));

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
