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

    [HttpGet("gateway/api/v1/users/me")]
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

    [HttpPut("gateway/api/v1/users/me")]
    [Authorize("authenticated")]
    [ProducesResponseType(typeof(ApiResponse<UserProfile>), StatusCodes.Status200OK)]
    public Task<IResult> UpdateMe([FromBody] UpdateProfileRequestDto body)
    {
        if (body.DisplayName is null && body.Email is null)
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "displayName hoặc email phải có"));
        if (body.Email is not null && !body.Email.Contains('@'))
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "email không hợp lệ", field: "Email"));

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

    [HttpPost("gateway/api/v1/users/me/seller-upgrade")]
    [Authorize("authenticated")]
    [ProducesResponseType(typeof(ApiResponse<SellerRegistrationInfo>), StatusCodes.Status200OK)]
    public Task<IResult> RequestSellerUpgrade([FromBody] SellerUpgradeRequestDto body)
    {
        if (string.IsNullOrWhiteSpace(body.ShopName))
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "shopName là bắt buộc", field: "ShopName"));
        if (body.ContactEmail is not null && !body.ContactEmail.Contains('@'))
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "contactEmail không hợp lệ", field: "ContactEmail"));

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

    [HttpGet("gateway/api/v1/admin/users")]
    [Authorize("staff")]
    [ProducesResponseType(typeof(ApiResponse<PagedData<UserProfile>>), StatusCodes.Status200OK)]
    public Task<IResult> ListUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? q = null, [FromQuery] string? role = null)
    {
        if (!ValidationHelpers.PageOk(page, pageSize))
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, ValidationHelpers.PageMessage, field: "Pagination"));

        return _dispatcher.DispatchPagedAsync(
            HttpContext,
            "users",
            "users",
            page,
            pageSize,
            o => _client.ListUsersAsync(new ListUsersRequest
            {
                Page = page,
                PageSize = pageSize,
                Q = q ?? "",
                Role = role ?? ""
            }, o),
            UserPage.Parser
        );
    }

    [HttpPost("gateway/api/v1/admin/sellers/{id:guid}/approve")]
    [Authorize("staff")]
    [ProducesResponseType(typeof(ApiResponse<SellerRegistrationInfo>), StatusCodes.Status200OK)]
    public Task<IResult> ApproveSeller([FromRoute] Guid id, [FromBody] ApproveSellerRequestDto body)
    {
        if (!body.Approve && string.IsNullOrWhiteSpace(body.Reason))
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "từ chối đơn đăng ký phải kèm reason", field: "Reason"));

        return _dispatcher.DispatchAsync(
            HttpContext,
            "users",
            "seller-approve",
            o => _client.ApproveSellerAsync(new ApproveSellerRequest
            {
                RegistrationId = id.ToString(),
                Approve = body.Approve,
                Reason = body.Reason ?? ""
            }, o),
            SellerRegistrationInfo.Parser,
            successMessage: "Seller application processed."
        );
    }

    [HttpPost("gateway/api/v1/admin/users")]
    [Authorize("admin")]
    [ProducesResponseType(typeof(ApiResponse<UserProfile>), StatusCodes.Status201Created)]
    public Task<IResult> CreateUser([FromBody] CreateUserRequestDto body)
    {
        var roles = body.Roles ?? Array.Empty<string>();
        if (string.IsNullOrWhiteSpace(body.Username) || !body.Email.Contains('@') || (body.Password?.Length ?? 0) < 8)
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "username, email hợp lệ và password >= 8 ký tự là bắt buộc"));
        if (roles.Any(r => !ValidationHelpers.KnownRoles.Contains(r.Trim().ToLowerInvariant())))
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "roles chỉ được chứa buyer|seller|staff|admin", field: "Roles"));

        var req = new CreateUserRequest
        {
            Username = body.Username,
            Email = body.Email,
            Password = body.Password,
            DisplayName = body.DisplayName ?? body.Username
        };
        req.Roles.AddRange(roles.Select(r => r.Trim().ToLowerInvariant()));

        return _dispatcher.DispatchAsync(
            HttpContext,
            "users",
            "user-create",
            o => _client.CreateUserAsync(req, o),
            UserProfile.Parser,
            successMessage: "User created successfully.",
            successStatusCode: StatusCodes.Status201Created
        );
    }

    [HttpPost("gateway/api/v1/admin/users/{id:guid}/roles")]
    [Authorize("admin")]
    [ProducesResponseType(typeof(ApiResponse<UserProfile>), StatusCodes.Status200OK)]
    public Task<IResult> AssignRoles([FromRoute] Guid id, [FromBody] AssignRolesRequestDto body)
    {
        var roles = body.Roles ?? Array.Empty<string>();
        if (roles.Length == 0)
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "ít nhất một role là bắt buộc", field: "Roles"));
        if (roles.Any(r => !ValidationHelpers.KnownRoles.Contains(r.Trim().ToLowerInvariant())))
            return Task.FromResult(ValidationHelpers.Bad(HttpContext, "roles chỉ được chứa buyer|seller|staff|admin", field: "Roles"));

        var req = new AssignRolesRequest { UserId = id.ToString() };
        req.Roles.AddRange(roles.Select(r => r.Trim().ToLowerInvariant()));

        return _dispatcher.DispatchAsync(
            HttpContext,
            "users",
            "user-roles",
            o => _client.AssignRolesAsync(req, o),
            UserProfile.Parser,
            successMessage: "Roles assigned successfully."
        );
    }

    [HttpGet("gateway/api/v1/admin/roles")]
    [Authorize("admin")]
    [ProducesResponseType(typeof(ApiResponse<RoleList>), StatusCodes.Status200OK)]
    public Task<IResult> ListRoles()
    {
        return _dispatcher.DispatchAsync(
            HttpContext,
            "users",
            "roles",
            o => _client.ListRolesAsync(new Empty(), o),
            RoleList.Parser,
            successMessage: "Roles retrieved successfully."
        );
    }

    [HttpGet("gateway/api/v1/admin/permissions")]
    [Authorize("admin")]
    [ProducesResponseType(typeof(ApiResponse<PermissionList>), StatusCodes.Status200OK)]
    public Task<IResult> ListPermissions()
    {
        return _dispatcher.DispatchAsync(
            HttpContext,
            "users",
            "permissions",
            o => _client.ListPermissionsAsync(new Empty(), o),
            PermissionList.Parser,
            successMessage: "Permissions retrieved successfully."
        );
    }
}
