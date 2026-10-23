namespace Nexus.Gateway.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.Gateway.Dispatching;
using Nexus.Gateway.Responses;
using Nexus.Gateway.Security;
using ShoppingCart.Contracts.V1;

[ApiController]
[Route("api/v1")]
[BackendTag("users")]
[EnableRateLimiting("api")]
public sealed class UsersController : ControllerBase
{
    private readonly IGatewayDispatcher _dispatcher;
    private readonly UsersApi.UsersApiClient _client;

    public UsersController(IGatewayDispatcher dispatcher, UsersApi.UsersApiClient client)
    {
        _dispatcher = dispatcher;
        _client = client;
    }

    [HttpGet("users/me")]
    [Authorize("authenticated")]
    [EnableRateLimiting("user-frequent")]
    [ProducesResponseType(typeof(ApiResponse<UserProfile>), StatusCodes.Status200OK)]
    public Task<IResult> GetMe()
    {
        return _dispatcher.DispatchAsync(
            HttpContext,
            "users",
            "profile",
            o => _client.GetProfileAsync(HttpContext.ToUserRequest(), o),
            UserProfile.Parser,
            successMessage: "User profile retrieved successfully."
        );
    }
}
