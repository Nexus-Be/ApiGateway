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
public sealed class AdminUsersController : ControllerBase
{
    private readonly IGatewayDispatcher _dispatcher;
    private readonly UsersApi.UsersApiClient _client;

    public AdminUsersController(IGatewayDispatcher dispatcher, UsersApi.UsersApiClient client)
    {
        _dispatcher = dispatcher;
        _client = client;
    }

    [HttpGet("admin/users")]
    [Authorize("staff")]
    [ProducesResponseType(typeof(ApiResponse<PagedData<UserProfile>>), StatusCodes.Status200OK)]
    public Task<IResult> ListUsers([FromQuery] PaginationRequest pagination, [FromQuery] string? q = null, [FromQuery] string? role = null)
    {
        pagination.EnsureValid();

        return _dispatcher.DispatchPagedAsync(
            HttpContext,
            "users",
            "users",
            pagination.Page,
            pagination.PageSize,
            o => _client.ListUsersAsync(new ListUsersRequest
            {
                Page = pagination.Page,
                PageSize = pagination.PageSize,
                Q = q ?? "",
                Role = role ?? ""
            }, o),
            UserPage.Parser
        );
    }

    [HttpPost("admin/sellers/{id:guid}/approve")]
    [Authorize("staff")]
    [ProducesResponseType(typeof(ApiResponse<SellerRegistrationInfo>), StatusCodes.Status200OK)]
    public Task<IResult> ApproveSeller([FromRoute] Guid id, [FromBody] ApproveSellerRequestDto body)
    {
        if (!body.Approve && string.IsNullOrWhiteSpace(body.Reason))
            throw new GatewayInputException("Rejecting a seller application requires a reason.", "Reason");

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

    [HttpPost("admin/users")]
    [Authorize("admin")]
    [ProducesResponseType(typeof(ApiResponse<UserProfile>), StatusCodes.Status201Created)]
    public Task<IResult> CreateUser([FromBody] CreateUserRequestDto body)
    {
        var roles = body.Roles ?? Array.Empty<string>();
        if (string.IsNullOrWhiteSpace(body.Username) || !body.Email.Contains('@') || (body.Password?.Length ?? 0) < 8)
            throw new GatewayInputException("Username, valid email, and password with at least 8 characters are required.");
        if (roles.Any(r => !ValidationHelpers.KnownRoles.Contains(r.Trim().ToLowerInvariant())))
            throw new GatewayInputException("Roles may only contain buyer|seller|staff|admin.", "Roles");

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

    [HttpPost("admin/users/{id:guid}/roles")]
    [Authorize("admin")]
    [ProducesResponseType(typeof(ApiResponse<UserProfile>), StatusCodes.Status200OK)]
    public Task<IResult> AssignRoles([FromRoute] Guid id, [FromBody] AssignRolesRequestDto body)
    {
        var roles = body.Roles ?? Array.Empty<string>();
        if (roles.Length == 0)
            throw new GatewayInputException("At least one role is required.", "Roles");
        if (roles.Any(r => !ValidationHelpers.KnownRoles.Contains(r.Trim().ToLowerInvariant())))
            throw new GatewayInputException("Roles may only contain buyer|seller|staff|admin.", "Roles");

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

    [HttpGet("admin/roles")]
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

    [HttpGet("admin/permissions")]
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
