namespace Nexus.Gateway.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.Gateway.Common;
using Nexus.Gateway.Dispatching;
using Nexus.Gateway.Dtos.Users;
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
    [ProducesResponseType(typeof(ApiResponse<UserProfile>), StatusCodes.Status200OK)]
    public Task<IResult> GetMe()
    {
        return _dispatcher.DispatchAsync(
            HttpContext,
            "users",
            "profile",
            o => _client.GetProfileAsync(HttpContext.ToUserRequest(), o),
            UserProfile.Parser,
            successMessage: "User retrieved successfully."
        );
    }

    [HttpPut("users/me")]
    [Authorize("authenticated")]
    [ProducesResponseType(typeof(ApiResponse<UserProfile>), StatusCodes.Status200OK)]
    public Task<IResult> UpdateMe([FromBody] UpdateProfileRequestDto body)
    {
        if (body.DisplayName is null && body.Email is null)
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "Either displayName or email is required."));
        if (body.Email is not null && !body.Email.Contains('@'))
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "Email is invalid.", field: "Email"));

        return _dispatcher.DispatchAsync(
            HttpContext,
            "users",
            "profile-update",
            o => _client.UpdateProfileAsync(new UpdateProfileRequest
            {
                UserId = HttpContext.ToUserRequest().UserId,
                DisplayName = body.DisplayName ?? "",
                Email = body.Email ?? ""
            }, o),
            UserProfile.Parser,
            successMessage: "Profile updated successfully."
        );
    }

    [HttpPost("users/me/seller-upgrade")]
    [Authorize("authenticated")]
    [ProducesResponseType(typeof(ApiResponse<SellerRegistrationInfo>), StatusCodes.Status200OK)]
    public Task<IResult> RequestSellerUpgrade([FromBody] SellerUpgradeRequestDto body)
    {
        if (string.IsNullOrWhiteSpace(body.ShopName))
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "ShopName is required.", field: "ShopName"));
        if (body.ContactEmail is not null && !body.ContactEmail.Contains('@'))
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "ContactEmail is invalid.", field: "ContactEmail"));

        return _dispatcher.DispatchAsync(
            HttpContext,
            "users",
            "seller-upgrade",
            o => _client.RequestSellerUpgradeAsync(new SellerUpgradeRequest
            {
                UserId = HttpContext.ToUserRequest().UserId,
                ShopName = body.ShopName,
                Description = body.Description ?? "",
                ContactEmail = body.ContactEmail ?? "",
                ContactPhone = body.ContactPhone ?? "",
                Address = body.Address ?? ""
            }, o),
            SellerRegistrationInfo.Parser,
            successMessage: "Seller upgrade requested successfully."
        );
    }
}
