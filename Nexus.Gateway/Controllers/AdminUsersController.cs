namespace Nexus.Gateway.Controllers;

using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.Gateway.Common;
using Nexus.Gateway.Dispatching;
using Nexus.Gateway.Dtos.User;
using Nexus.Gateway.Responses;
using Nexus.Gateway.Security;

[ApiController]
[Route("api/v1/admin")]
[BackendTag("users")]
[EnableRateLimiting("api")]
public sealed class AdminUsersController : GatewayBaseController
{
    private const string Service = "users";

    public AdminUsersController(IGatewayDispatcher dispatcher)
        : base(dispatcher)
    {
    }

    [HttpGet("seller-registrations")]
    [Authorize(GatewayPolicies.Staff)]
    [ProducesResponseType(typeof(ApiResponse<AdminSellerRegistrationListResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> ListSellerRegistrations(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] string status = "")
    {
        UserValidation.EnsurePaged(page, limit);
        UserValidation.EnsureOptionalSellerRegistrationStatus(status);

        return ForwardPagedAsync(
            Service,
            "/api/v1/admin/seller-registrations",
            page,
            limit,
            HttpMethod.Get,
            mockSeed: "admin-seller-registrations",
            queryParams: new Dictionary<string, string?>
            {
                ["page"] = page.ToString(),
                ["limit"] = limit.ToString(),
                ["status"] = status
            }
        );
    }

    [HttpGet("seller-registrations/{registrationId}")]
    [Authorize(GatewayPolicies.Staff)]
    [ProducesResponseType(typeof(ApiResponse<AdminSellerRegistrationDetailResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> GetSellerRegistrationDetail([FromRoute] string registrationId)
    {
        UserValidation.EnsureRequired(registrationId, "RegistrationId");

        return ForwardAsync(
            Service,
            $"/api/v1/admin/seller-registrations/{registrationId}",
            HttpMethod.Get,
            mockSeed: "admin-seller-registration-detail",
            successMessage: "Registration detail retrieved successfully."
        );
    }

    [HttpPut("seller-registrations/{registrationId}/approve")]
    [Authorize(GatewayPolicies.Staff)]
    [ProducesResponseType(typeof(ApiResponse<ApproveSellerRegistrationResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> ApproveSellerRegistration(
        [FromRoute] string registrationId,
        [FromBody] ApproveSellerRegistrationDto body)
    {
        UserValidation.EnsureRequired(registrationId, "RegistrationId");

        return ForwardAsync(
            Service,
            $"/api/v1/admin/seller-registrations/{registrationId}/approve",
            HttpMethod.Put,
            body: body,
            mockSeed: "admin-seller-registration-approve",
            successMessage: "Seller registration approved successfully."
        );
    }

    [HttpPut("seller-registrations/{registrationId}/reject")]
    [Authorize(GatewayPolicies.Staff)]
    [ProducesResponseType(typeof(ApiResponse<RejectSellerRegistrationResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> RejectSellerRegistration(
        [FromRoute] string registrationId,
        [FromBody] RejectSellerRegistrationDto body)
    {
        UserValidation.EnsureRequired(registrationId, "RegistrationId");
        UserValidation.EnsureRequired(body.RejectionReason, "RejectionReason", "Rejection reason");

        return ForwardAsync(
            Service,
            $"/api/v1/admin/seller-registrations/{registrationId}/reject",
            HttpMethod.Put,
            body: body,
            mockSeed: "admin-seller-registration-reject",
            successMessage: "Seller registration rejected successfully."
        );
    }

    [HttpGet("reports")]
    [Authorize(GatewayPolicies.Staff)]
    [ProducesResponseType(typeof(ApiResponse<AdminReportListResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> ListReports(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] string status = "",
        [FromQuery] string violationType = "")
    {
        UserValidation.EnsurePaged(page, limit);
        UserValidation.EnsureOptionalReportStatus(status);
        UserValidation.EnsureOptionalReportViolationType(violationType);

        return ForwardPagedAsync(
            Service,
            "/api/v1/admin/reports",
            page,
            limit,
            HttpMethod.Get,
            mockSeed: "admin-reports",
            queryParams: new Dictionary<string, string?>
            {
                ["page"] = page.ToString(),
                ["limit"] = limit.ToString(),
                ["status"] = status,
                ["violationType"] = violationType
            }
        );
    }

    [HttpPut("reports/{reportId}/resolve")]
    [Authorize(GatewayPolicies.Staff)]
    [ProducesResponseType(typeof(ApiResponse<ResolveAdminReportResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> ResolveReport(
        [FromRoute] string reportId,
        [FromBody] ResolveAdminReportDto body)
    {
        UserValidation.EnsureRequired(reportId, "ReportId");
        UserValidation.EnsureRequired(body.Action, "Action");

        return ForwardAsync(
            Service,
            $"/api/v1/admin/reports/{reportId}/resolve",
            HttpMethod.Put,
            body: body,
            mockSeed: "admin-report-resolve",
            successMessage: "Report resolved successfully."
        );
    }

    [HttpPut("reports/{reportId}/dismiss")]
    [Authorize(GatewayPolicies.Staff)]
    [ProducesResponseType(typeof(ApiResponse<DismissAdminReportResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> DismissReport(
        [FromRoute] string reportId,
        [FromBody] DismissAdminReportDto body)
    {
        UserValidation.EnsureRequired(reportId, "ReportId");
        UserValidation.EnsureRequired(body.Reason, "Reason");

        return ForwardAsync(
            Service,
            $"/api/v1/admin/reports/{reportId}/dismiss",
            HttpMethod.Put,
            body: body,
            mockSeed: "admin-report-dismiss",
            successMessage: "Report dismissed successfully."
        );
    }

    [HttpPut("reviews/{reviewId}/hide")]
    [Authorize(GatewayPolicies.Staff)]
    [ProducesResponseType(typeof(ApiResponse<HideAdminReviewResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> HideReview(
        [FromRoute] string reviewId,
        [FromBody] HideAdminReviewDto body)
    {
        UserValidation.EnsureRequired(reviewId, "ReviewId");
        UserValidation.EnsureRequired(body.Reason, "Reason");

        return ForwardAsync(
            Service,
            $"/api/v1/admin/reviews/{reviewId}/hide",
            HttpMethod.Put,
            body: body,
            mockSeed: "admin-review-hide",
            successMessage: "Review visibility updated successfully."
        );
    }

    [HttpGet("user")]
    [Authorize(GatewayPolicies.Staff)]
    [ProducesResponseType(typeof(ApiResponse<AdminUserListResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> ListUsers(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] string keyword = "",
        [FromQuery] string role = "",
        [FromQuery] string status = "")
    {
        UserValidation.EnsurePaged(page, limit);
        UserValidation.EnsureOptionalRole(role);
        UserValidation.EnsureOptionalUserStatus(status);

        return ForwardPagedAsync(
            Service,
            "/api/v1/admin/users",
            page,
            limit,
            HttpMethod.Get,
            mockSeed: "admin-users",
            queryParams: new Dictionary<string, string?>
            {
                ["page"] = page.ToString(),
                ["limit"] = limit.ToString(),
                ["keyword"] = keyword,
                ["role"] = role,
                ["status"] = status
            }
        );
    }

    [HttpPost("user")]
    [Authorize(GatewayPolicies.Admin)]
    [ProducesResponseType(typeof(ApiResponse<AdminUserCreatedResponseDto>), StatusCodes.Status201Created)]
    public Task<IResult> CreateUser([FromBody] CreateAdminUserDto body)
    {
        UserValidation.EnsureEmail(body.Email);
        UserValidation.EnsurePassword(body.Password);
        UserValidation.EnsureRequired(body.FirstName, "FirstName", "First name");
        UserValidation.EnsureRequired(body.LastName, "LastName", "Last name");

        return ForwardAsync(
            Service,
            "/api/v1/admin/users",
            HttpMethod.Post,
            body: body,
            mockSeed: "admin-user-create",
            successStatusCode: StatusCodes.Status201Created,
            successMessage: "User created successfully."
        );
    }

    [HttpGet("user/{userId}")]
    [Authorize(GatewayPolicies.Staff)]
    [ProducesResponseType(typeof(ApiResponse<AdminUserDetailResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> GetUserDetail([FromRoute] string userId)
    {
        UserValidation.EnsureUuid(userId, "UserId");

        return ForwardAsync(
            Service,
            $"/api/v1/admin/users/{userId}",
            HttpMethod.Get,
            mockSeed: "admin-user-detail",
            successMessage: "User detail retrieved successfully."
        );
    }

    [HttpPut("user/{userId}")]
    [Authorize(GatewayPolicies.Admin)]
    [ProducesResponseType(typeof(ApiResponse<AdminUserUpdatedResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> UpdateUser(
        [FromRoute] string userId,
        [FromBody] UpdateAdminUserDto body)
    {
        UserValidation.EnsureUuid(userId, "UserId");
        UserValidation.EnsureRequired(body.Reason, "Reason");

        return ForwardAsync(
            Service,
            $"/api/v1/admin/users/{userId}",
            HttpMethod.Put,
            body: body,
            mockSeed: "admin-user-update",
            successMessage: "User updated successfully."
        );
    }

    [HttpDelete("user/{userId}")]
    [Authorize(GatewayPolicies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IResult> DeleteUser(
        [FromRoute] string userId,
        [FromBody] DeleteAdminUserDto body)
    {
        UserValidation.EnsureUuid(userId, "UserId");
        UserValidation.EnsureRequired(body.Reason, "Reason");

        return ForwardNoContentAsync(
            Service,
            $"/api/v1/admin/users/{userId}",
            HttpMethod.Delete,
            body: body,
            mockSeed: "admin-user-delete"
        );
    }

    [HttpPatch("user/{userId}/ban")]
    [Authorize(GatewayPolicies.StaffOrAdmin)]
    [ProducesResponseType(typeof(ApiResponse<AdminUserBanResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> BanUser(
        [FromRoute] string userId,
        [FromBody] BanAdminUserDto body)
    {
        UserValidation.EnsureUuid(userId, "UserId");
        UserValidation.EnsureRequired(body.Reason, "Reason");

        return ForwardAsync(
            Service,
            $"/api/v1/admin/users/{userId}/ban",
            HttpMethod.Patch,
            body: body,
            mockSeed: "admin-user-ban",
            successMessage: "User banned successfully."
        );
    }

    [HttpPatch("user/{userId}/unban")]
    [Authorize(GatewayPolicies.StaffOrAdmin)]
    [ProducesResponseType(typeof(ApiResponse<AdminUserUnbanResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> UnbanUser(
        [FromRoute] string userId,
        [FromBody] UnbanAdminUserDto body)
    {
        UserValidation.EnsureUuid(userId, "UserId");
        UserValidation.EnsureRequired(body.Reason, "Reason");

        return ForwardAsync(
            Service,
            $"/api/v1/admin/users/{userId}/unban",
            HttpMethod.Patch,
            body: body,
            mockSeed: "admin-user-unban",
            successMessage: "User unbanned successfully."
        );
    }

    [HttpGet("sellers/{sellerId}")]
    [Authorize(GatewayPolicies.Staff)]
    [ProducesResponseType(typeof(ApiResponse<AdminSellerDetailResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> GetSellerDetail([FromRoute] string sellerId)
    {
        UserValidation.EnsureUuid(sellerId, "SellerId");

        return ForwardAsync(
            Service,
            $"/api/v1/admin/sellers/{sellerId}",
            HttpMethod.Get,
            mockSeed: "admin-seller-detail",
            successMessage: "Seller detail retrieved successfully."
        );
    }

    [HttpPut("user/{userId}/roles")]
    [Authorize(GatewayPolicies.Admin)]
    [ProducesResponseType(typeof(ApiResponse<AdminUserRolesResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> AssignRoles(
        [FromRoute] string userId,
        [FromBody] AssignAdminUserRolesDto body)
    {
        UserValidation.EnsureUuid(userId, "UserId");
        UserValidation.EnsureRequired(body.Reason, "Reason");

        return ForwardAsync(
            Service,
            $"/api/v1/admin/users/{userId}/roles",
            HttpMethod.Put,
            body: body,
            mockSeed: "admin-user-roles-assign",
            successMessage: "User roles updated successfully."
        );
    }

    [HttpPatch("user/{userId}/reputation")]
    [Authorize(GatewayPolicies.Admin)]
    [ProducesResponseType(typeof(ApiResponse<AdminUserReputationResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> AdjustReputation(
        [FromRoute] string userId,
        [FromBody] AdjustAdminUserReputationDto body)
    {
        UserValidation.EnsureUuid(userId, "UserId");
        UserValidation.EnsureRequired(body.Reason, "Reason");

        return ForwardAsync(
            Service,
            $"/api/v1/admin/users/{userId}/reputation",
            HttpMethod.Patch,
            body: body,
            mockSeed: "admin-user-reputation-adjust",
            successMessage: "User reputation adjusted successfully."
        );
    }

    [HttpGet("roles")]
    [Authorize(GatewayPolicies.StaffOrAdmin)]
    [ProducesResponseType(typeof(ApiResponse<AdminRoleListResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> ListRoles()
    {
        return ForwardAsync(
            Service,
            "/api/v1/admin/roles",
            HttpMethod.Get,
            mockSeed: "admin-roles",
            successMessage: "Roles retrieved successfully."
        );
    }

    [HttpGet("permissions")]
    [Authorize(GatewayPolicies.StaffOrAdmin)]
    [ProducesResponseType(typeof(ApiResponse<AdminPermissionListResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> ListPermissions()
    {
        return ForwardAsync(
            Service,
            "/api/v1/admin/permissions",
            HttpMethod.Get,
            mockSeed: "admin-permissions",
            successMessage: "Permissions retrieved successfully."
        );
    }

    [HttpPut("roles/{roleId}/permissions")]
    [Authorize(GatewayPolicies.Admin)]
    [ProducesResponseType(typeof(ApiResponse<AdminRolePermissionsUpdatedResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> UpdateRolePermissions(
        [FromRoute] string roleId,
        [FromBody] UpdateAdminRolePermissionsDto body)
    {
        return ForwardAsync(
            Service,
            $"/api/v1/admin/roles/{roleId}/permissions",
            HttpMethod.Put,
            body: body,
            mockSeed: "admin-role-permissions-update",
            successMessage: "Role permissions updated successfully."
        );
    }

    [HttpGet("audit-logs")]
    [Authorize(GatewayPolicies.StaffOrAdmin)]
    [ProducesResponseType(typeof(ApiResponse<AdminAuditLogListResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> ListAuditLogs(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] string actorId = "",
        [FromQuery] string action = "",
        [FromQuery] string fromDate = "",
        [FromQuery] string toDate = "")
    {
        UserValidation.EnsurePaged(page, limit);

        return ForwardPagedAsync(
            Service,
            "/api/v1/admin/audit-logs",
            page,
            limit,
            HttpMethod.Get,
            mockSeed: "admin-audit-logs",
            queryParams: new Dictionary<string, string?>
            {
                ["page"] = page.ToString(),
                ["limit"] = limit.ToString(),
                ["actorId"] = actorId,
                ["action"] = action,
                ["fromDate"] = fromDate,
                ["toDate"] = toDate
            }
        );
    }

    [HttpGet("configs")]
    [Authorize(GatewayPolicies.StaffOrAdmin)]
    [ProducesResponseType(typeof(ApiResponse<AdminConfigListResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> ListConfigs()
    {
        return ForwardAsync(
            Service,
            "/api/v1/admin/configs",
            HttpMethod.Get,
            mockSeed: "admin-configs",
            successMessage: "Configs retrieved successfully."
        );
    }

    [HttpPut("configs")]
    [Authorize(GatewayPolicies.Admin)]
    [ProducesResponseType(typeof(ApiResponse<AdminConfigUpdatedResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> UpdateConfig(
        [FromBody] UpdateAdminConfigDto body,
        [FromRoute] string? key = null)
    {
        var cfgKey = UserValidation.EnsureRequired(key ?? body.Key, "Key");
        UserValidation.EnsureRequired(body.Value, "Value");
        UserValidation.EnsureRequired(body.Reason, "Reason");

        return ForwardAsync(
            Service,
            $"/api/v1/admin/configs/{cfgKey}",
            HttpMethod.Put,
            body: body,
            mockSeed: "admin-config-update",
            successMessage: "Config updated successfully."
        );
    }
}
