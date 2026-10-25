namespace Nexus.Gateway.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.Gateway.Common;
using Nexus.Gateway.Dispatching;
using Nexus.Gateway.Dtos.User;
using Nexus.Gateway.Responses;
using Nexus.Gateway.Security;
using ShoppingCart.Contracts.V1;

[ApiController]
[Route("api/v1/admin")]
[BackendTag("users")]
[EnableRateLimiting("api")]
public sealed class AdminUsersController : ControllerBase
{
    private const string Service = "users";
    private readonly IGatewayDispatcher _dispatcher;
    private readonly UserApi.UserApiClient _client;

    public AdminUsersController(IGatewayDispatcher dispatcher, UserApi.UserApiClient client)
    {
        _dispatcher = dispatcher;
        _client = client;
    }

    [HttpGet("seller-registrations")]
    [Authorize(GatewayPolicies.Staff)]
    [ProducesResponseType(typeof(ApiResponse<AdminSellerRegistrationListResponse>), StatusCodes.Status200OK)]
    public Task<IResult> ListSellerRegistrations(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] string status = "")
    {
        UserValidation.EnsurePaged(page, limit);

        return _dispatcher.DispatchPagedAsync(
            HttpContext,
            Service,
            "admin-seller-registrations",
            page,
            limit,
            o => _client.ListAdminSellerRegistrationsAsync(new ListAdminSellerRegistrationsRequest
            {
                Page = page,
                Limit = limit,
                Status = status ?? string.Empty,
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            AdminSellerRegistrationListResponse.Parser
        );
    }

    [HttpGet("seller-registrations/{registrationId}")]
    [Authorize(GatewayPolicies.Staff)]
    [ProducesResponseType(typeof(ApiResponse<AdminSellerRegistrationDetailResponse>), StatusCodes.Status200OK)]
    public Task<IResult> GetSellerRegistrationDetail([FromRoute] string registrationId)
    {
        UserValidation.EnsureRequired(registrationId, "RegistrationId");

        return _dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "admin-seller-registration-detail",
            o => _client.GetAdminSellerRegistrationDetailAsync(new GetAdminSellerRegistrationDetailRequest
            {
                RegistrationId = registrationId,
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            AdminSellerRegistrationDetailResponse.Parser,
            successMessage: "Registration detail retrieved successfully."
        );
    }

    [HttpPut("seller-registrations/{registrationId}/approve")]
    [Authorize(GatewayPolicies.Staff)]
    [ProducesResponseType(typeof(ApiResponse<ApproveSellerRegistrationResponse>), StatusCodes.Status200OK)]
    public Task<IResult> ApproveSellerRegistration(
        [FromRoute] string registrationId,
        [FromBody] ApproveSellerRegistrationDto body)
    {
        UserValidation.EnsureRequired(registrationId, "RegistrationId");

        return _dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "admin-seller-registration-approve",
            o => _client.ApproveSellerRegistrationAsync(new ApproveSellerRegistrationRequest
            {
                RegistrationId = registrationId,
                Note = body.Note ?? string.Empty,
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            ApproveSellerRegistrationResponse.Parser,
            successMessage: "Seller registration approved successfully."
        );
    }

    [HttpPut("seller-registrations/{registrationId}/reject")]
    [Authorize(GatewayPolicies.Staff)]
    [ProducesResponseType(typeof(ApiResponse<RejectSellerRegistrationResponse>), StatusCodes.Status200OK)]
    public Task<IResult> RejectSellerRegistration(
        [FromRoute] string registrationId,
        [FromBody] RejectSellerRegistrationDto body)
    {
        UserValidation.EnsureRequired(registrationId, "RegistrationId");
        var reason = UserValidation.EnsureRequired(body.RejectionReason, "RejectionReason", "Rejection reason");

        return _dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "admin-seller-registration-reject",
            o => _client.RejectSellerRegistrationAsync(new RejectSellerRegistrationRequest
            {
                RegistrationId = registrationId,
                RejectionReason = reason,
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            RejectSellerRegistrationResponse.Parser,
            successMessage: "Seller registration rejected successfully."
        );
    }

    [HttpGet("reports")]
    [Authorize(GatewayPolicies.Staff)]
    [ProducesResponseType(typeof(ApiResponse<AdminReportListResponse>), StatusCodes.Status200OK)]
    public Task<IResult> ListReports(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] string status = "",
        [FromQuery] string violationType = "")
    {
        UserValidation.EnsurePaged(page, limit);

        return _dispatcher.DispatchPagedAsync(
            HttpContext,
            Service,
            "admin-reports",
            page,
            limit,
            o => _client.ListAdminReportsAsync(new ListAdminReportsRequest
            {
                Page = page,
                Limit = limit,
                Status = status ?? string.Empty,
                ViolationType = violationType ?? string.Empty,
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            AdminReportListResponse.Parser
        );
    }

    [HttpPut("reports/{reportId}/resolve")]
    [Authorize(GatewayPolicies.Staff)]
    [ProducesResponseType(typeof(ApiResponse<ResolveAdminReportResponse>), StatusCodes.Status200OK)]
    public Task<IResult> ResolveReport(
        [FromRoute] string reportId,
        [FromBody] ResolveAdminReportDto body)
    {
        UserValidation.EnsureRequired(reportId, "ReportId");
        var action = UserValidation.EnsureRequired(body.Action, "Action");

        return _dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "admin-report-resolve",
            o => _client.ResolveAdminReportAsync(new ResolveAdminReportRequest
            {
                ReportId = reportId,
                Action = action,
                PointsDeducted = body.PointsDeducted ?? 0,
                ResolutionNote = body.ResolutionNote ?? string.Empty,
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            ResolveAdminReportResponse.Parser,
            successMessage: "Report resolved successfully."
        );
    }

    [HttpPut("reports/{reportId}/dismiss")]
    [Authorize(GatewayPolicies.Staff)]
    [ProducesResponseType(typeof(ApiResponse<DismissAdminReportResponse>), StatusCodes.Status200OK)]
    public Task<IResult> DismissReport(
        [FromRoute] string reportId,
        [FromBody] DismissAdminReportDto body)
    {
        UserValidation.EnsureRequired(reportId, "ReportId");
        var reason = UserValidation.EnsureRequired(body.Reason, "Reason");

        return _dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "admin-report-dismiss",
            o => _client.DismissAdminReportAsync(new DismissAdminReportRequest
            {
                ReportId = reportId,
                Reason = reason,
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            DismissAdminReportResponse.Parser,
            successMessage: "Report dismissed successfully."
        );
    }

    [HttpPut("reviews/{reviewId}/hide")]
    [Authorize(GatewayPolicies.Staff)]
    [ProducesResponseType(typeof(ApiResponse<HideAdminReviewResponse>), StatusCodes.Status200OK)]
    public Task<IResult> HideReview(
        [FromRoute] string reviewId,
        [FromBody] HideAdminReviewDto body)
    {
        UserValidation.EnsureRequired(reviewId, "ReviewId");
        var reason = UserValidation.EnsureRequired(body.Reason, "Reason");

        return _dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "admin-review-hide",
            o => _client.HideAdminReviewAsync(new HideAdminReviewRequest
            {
                ReviewId = reviewId,
                Reason = reason,
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            HideAdminReviewResponse.Parser,
            successMessage: "Review visibility updated successfully."
        );
    }

    [HttpGet("user")]
    [HttpGet("users")]
    [Authorize(GatewayPolicies.Staff)]
    [ProducesResponseType(typeof(ApiResponse<AdminUserListResponse>), StatusCodes.Status200OK)]
    public Task<IResult> ListUsers(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] string keyword = "",
        [FromQuery] string role = "",
        [FromQuery] string status = "")
    {
        UserValidation.EnsurePaged(page, limit);

        return _dispatcher.DispatchPagedAsync(
            HttpContext,
            Service,
            "admin-users",
            page,
            limit,
            o => _client.ListAdminUsersAsync(new ListAdminUsersRequest
            {
                Page = page,
                Limit = limit,
                Keyword = keyword ?? string.Empty,
                Role = role ?? string.Empty,
                Status = status ?? string.Empty,
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            AdminUserListResponse.Parser
        );
    }

    [HttpPost("user")]
    [HttpPost("users")]
    [Authorize(GatewayPolicies.Admin)]
    [ProducesResponseType(typeof(ApiResponse<AdminUserCreatedResponse>), StatusCodes.Status201Created)]
    public Task<IResult> CreateUser([FromBody] CreateAdminUserDto body)
    {
        var email = UserValidation.EnsureEmail(body.Email);
        var password = UserValidation.EnsurePassword(body.Password);
        var firstName = UserValidation.EnsureRequired(body.FirstName, "FirstName", "First name");
        var lastName = UserValidation.EnsureRequired(body.LastName, "LastName", "Last name");

        var req = new CreateAdminUserRequest
        {
            Email = email,
            Password = password,
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = body.PhoneNumber ?? string.Empty,
            Caller = UserValidation.Caller(HttpContext)
        };
        if (body.RoleCodes != null) req.RoleCodes.AddRange(body.RoleCodes);

        return _dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "admin-user-create",
            o => _client.CreateAdminUserAsync(req, o),
            AdminUserCreatedResponse.Parser,
            successStatusCode: StatusCodes.Status201Created,
            successMessage: "User created successfully."
        );
    }

    [HttpGet("user/{userId}")]
    [HttpGet("users/{userId}")]
    [Authorize(GatewayPolicies.Staff)]
    [ProducesResponseType(typeof(ApiResponse<AdminUserDetailResponse>), StatusCodes.Status200OK)]
    public Task<IResult> GetUserDetail([FromRoute] string userId)
    {
        UserValidation.EnsureUuid(userId, "UserId");

        return _dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "admin-user-detail",
            o => _client.GetAdminUserDetailAsync(new GetAdminUserDetailRequest
            {
                UserId = userId,
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            AdminUserDetailResponse.Parser,
            successMessage: "User detail retrieved successfully."
        );
    }

    [HttpPut("user/{userId}")]
    [HttpPut("users/{userId}")]
    [Authorize(GatewayPolicies.Admin)]
    [ProducesResponseType(typeof(ApiResponse<AdminUserUpdatedResponse>), StatusCodes.Status200OK)]
    public Task<IResult> UpdateUser(
        [FromRoute] string userId,
        [FromBody] UpdateAdminUserDto body)
    {
        UserValidation.EnsureUuid(userId, "UserId");
        var reason = UserValidation.EnsureRequired(body.Reason, "Reason");

        return _dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "admin-user-update",
            o => _client.UpdateAdminUserAsync(new UpdateAdminUserRequest
            {
                UserId = userId,
                FirstName = body.FirstName ?? string.Empty,
                LastName = body.LastName ?? string.Empty,
                PhoneNumber = body.PhoneNumber ?? string.Empty,
                Status = body.Status ?? string.Empty,
                Reason = reason,
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            AdminUserUpdatedResponse.Parser,
            successMessage: "User updated successfully."
        );
    }

    [HttpDelete("user/{userId}")]
    [HttpDelete("users/{userId}")]
    [Authorize(GatewayPolicies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IResult> DeleteUser(
        [FromRoute] string userId,
        [FromBody] DeleteAdminUserDto body)
    {
        UserValidation.EnsureUuid(userId, "UserId");
        var reason = UserValidation.EnsureRequired(body.Reason, "Reason");

        return _dispatcher.DispatchNoContentAsync(
            HttpContext,
            Service,
            "admin-user-delete",
            o => _client.DeleteAdminUserAsync(new DeleteAdminUserRequest
            {
                UserId = userId,
                Reason = reason,
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            AdminUserDeletedResponse.Parser
        );
    }

    [HttpPatch("user/{userId}/ban")]
    [HttpPatch("users/{userId}/ban")]
    [Authorize(GatewayPolicies.Admin)]
    [ProducesResponseType(typeof(ApiResponse<AdminUserBanResponse>), StatusCodes.Status200OK)]
    public Task<IResult> BanUser(
        [FromRoute] string userId,
        [FromBody] BanAdminUserDto body)
    {
        UserValidation.EnsureUuid(userId, "UserId");
        var reason = UserValidation.EnsureRequired(body.Reason, "Reason");

        return _dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "admin-user-ban",
            o => _client.BanAdminUserAsync(new BanAdminUserRequest
            {
                UserId = userId,
                Reason = reason,
                BanDurationDays = body.BanDurationDays ?? 30,
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            AdminUserBanResponse.Parser,
            successMessage: "User banned successfully."
        );
    }

    [HttpPatch("user/{userId}/unban")]
    [HttpPatch("users/{userId}/unban")]
    [Authorize(GatewayPolicies.Admin)]
    [ProducesResponseType(typeof(ApiResponse<AdminUserUnbanResponse>), StatusCodes.Status200OK)]
    public Task<IResult> UnbanUser(
        [FromRoute] string userId,
        [FromBody] UnbanAdminUserDto body)
    {
        UserValidation.EnsureUuid(userId, "UserId");
        var reason = UserValidation.EnsureRequired(body.Reason, "Reason");

        return _dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "admin-user-unban",
            o => _client.UnbanAdminUserAsync(new UnbanAdminUserRequest
            {
                UserId = userId,
                Reason = reason,
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            AdminUserUnbanResponse.Parser,
            successMessage: "User unbanned successfully."
        );
    }

    [HttpGet("sellers/{sellerId}")]
    [Authorize(GatewayPolicies.Staff)]
    [ProducesResponseType(typeof(ApiResponse<AdminSellerDetailResponse>), StatusCodes.Status200OK)]
    public Task<IResult> GetSellerDetail([FromRoute] string sellerId)
    {
        UserValidation.EnsureUuid(sellerId, "SellerId");

        return _dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "admin-seller-detail",
            o => _client.GetAdminSellerDetailAsync(new GetAdminSellerDetailRequest
            {
                SellerId = sellerId,
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            AdminSellerDetailResponse.Parser,
            successMessage: "Seller detail retrieved successfully."
        );
    }

    [HttpPut("user/{userId}/roles")]
    [HttpPut("users/{userId}/roles")]
    [Authorize(GatewayPolicies.Admin)]
    [ProducesResponseType(typeof(ApiResponse<AdminUserRolesResponse>), StatusCodes.Status200OK)]
    public Task<IResult> AssignRoles(
        [FromRoute] string userId,
        [FromBody] AssignAdminUserRolesDto body)
    {
        UserValidation.EnsureUuid(userId, "UserId");
        var reason = UserValidation.EnsureRequired(body.Reason, "Reason");

        var req = new AssignAdminUserRolesRequest
        {
            UserId = userId,
            Reason = reason,
            Caller = UserValidation.Caller(HttpContext)
        };
        if (body.RoleCodes != null) req.RoleCodes.AddRange(body.RoleCodes);
        if (body.RoleIds != null) req.RoleIds.AddRange(body.RoleIds);

        return _dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "admin-user-roles-assign",
            o => _client.AssignAdminUserRolesAsync(req, o),
            AdminUserRolesResponse.Parser,
            successMessage: "User roles updated successfully."
        );
    }

    [HttpPatch("user/{userId}/reputation")]
    [HttpPatch("users/{userId}/reputation")]
    [Authorize(GatewayPolicies.Admin)]
    [ProducesResponseType(typeof(ApiResponse<AdminUserReputationResponse>), StatusCodes.Status200OK)]
    public Task<IResult> AdjustReputation(
        [FromRoute] string userId,
        [FromBody] AdjustAdminUserReputationDto body)
    {
        UserValidation.EnsureUuid(userId, "UserId");
        var reason = UserValidation.EnsureRequired(body.Reason, "Reason");

        return _dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "admin-user-reputation-adjust",
            o => _client.AdjustAdminUserReputationAsync(new AdjustAdminUserReputationRequest
            {
                UserId = userId,
                Delta = body.Delta ?? 0,
                Reason = reason,
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            AdminUserReputationResponse.Parser,
            successMessage: "User reputation adjusted successfully."
        );
    }

    [HttpGet("roles")]
    [Authorize(GatewayPolicies.Admin)]
    [ProducesResponseType(typeof(ApiResponse<AdminRoleListResponse>), StatusCodes.Status200OK)]
    public Task<IResult> ListRoles()
    {
        return _dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "admin-roles",
            o => _client.ListAdminRolesAsync(new ListAdminRolesRequest
            {
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            AdminRoleListResponse.Parser,
            successMessage: "Roles retrieved successfully."
        );
    }

    [HttpGet("permissions")]
    [Authorize(GatewayPolicies.Admin)]
    [ProducesResponseType(typeof(ApiResponse<AdminPermissionListResponse>), StatusCodes.Status200OK)]
    public Task<IResult> ListPermissions()
    {
        return _dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "admin-permissions",
            o => _client.ListAdminPermissionsAsync(new ListAdminPermissionsRequest
            {
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            AdminPermissionListResponse.Parser,
            successMessage: "Permissions retrieved successfully."
        );
    }

    [HttpPut("roles/{roleId:int}/permissions")]
    [Authorize(GatewayPolicies.Admin)]
    [ProducesResponseType(typeof(ApiResponse<AdminRolePermissionsUpdatedResponse>), StatusCodes.Status200OK)]
    public Task<IResult> UpdateRolePermissions(
        [FromRoute] int roleId,
        [FromBody] UpdateAdminRolePermissionsDto body)
    {
        var req = new UpdateAdminRolePermissionsRequest
        {
            RoleId = roleId,
            Description = body.Description ?? string.Empty,
            Caller = UserValidation.Caller(HttpContext)
        };
        if (body.PermissionIds != null) req.PermissionIds.AddRange(body.PermissionIds);

        return _dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "admin-role-permissions-update",
            o => _client.UpdateAdminRolePermissionsAsync(req, o),
            AdminRolePermissionsUpdatedResponse.Parser,
            successMessage: "Role permissions updated successfully."
        );
    }

    [HttpGet("audit-logs")]
    [Authorize(GatewayPolicies.Admin)]
    [ProducesResponseType(typeof(ApiResponse<AdminAuditLogListResponse>), StatusCodes.Status200OK)]
    public Task<IResult> ListAuditLogs(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] string actorId = "",
        [FromQuery] string action = "",
        [FromQuery] string fromDate = "",
        [FromQuery] string toDate = "")
    {
        UserValidation.EnsurePaged(page, limit);

        return _dispatcher.DispatchPagedAsync(
            HttpContext,
            Service,
            "admin-audit-logs",
            page,
            limit,
            o => _client.ListAdminAuditLogsAsync(new ListAdminAuditLogsRequest
            {
                Page = page,
                Limit = limit,
                ActorId = actorId ?? string.Empty,
                Action = action ?? string.Empty,
                FromDate = fromDate ?? string.Empty,
                ToDate = toDate ?? string.Empty,
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            AdminAuditLogListResponse.Parser
        );
    }

    [HttpGet("configs")]
    [Authorize(GatewayPolicies.Admin)]
    [ProducesResponseType(typeof(ApiResponse<AdminConfigListResponse>), StatusCodes.Status200OK)]
    public Task<IResult> ListConfigs()
    {
        return _dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "admin-configs",
            o => _client.ListAdminConfigsAsync(new ListAdminConfigsRequest
            {
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            AdminConfigListResponse.Parser,
            successMessage: "Configs retrieved successfully."
        );
    }

    [HttpPut("configs/{key}")]
    [Authorize(GatewayPolicies.Admin)]
    [ProducesResponseType(typeof(ApiResponse<AdminConfigUpdatedResponse>), StatusCodes.Status200OK)]
    public Task<IResult> UpdateConfig(
        [FromRoute] string key,
        [FromBody] UpdateAdminConfigDto body)
    {
        var cfgKey = UserValidation.EnsureRequired(key, "Key");
        var cfgVal = UserValidation.EnsureRequired(body.Value, "Value");
        var reason = UserValidation.EnsureRequired(body.Reason, "Reason");

        return _dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "admin-config-update",
            o => _client.UpdateAdminConfigAsync(new UpdateAdminConfigRequest
            {
                Key = cfgKey,
                Value = cfgVal,
                Reason = reason,
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            AdminConfigUpdatedResponse.Parser,
            successMessage: "Config updated successfully."
        );
    }
}
