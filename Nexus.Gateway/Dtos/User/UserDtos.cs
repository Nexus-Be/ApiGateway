namespace Nexus.Gateway.Dtos.User;

using Microsoft.AspNetCore.Http;

// ============================================================
// USER REQUEST DTOs — aligned with flow_mock_excel.xlsx
// ============================================================

// ---------- ME ----------

/// <summary>USER-02: PATCH /api/v1/me - update personal profile</summary>
public sealed class UpdateMyProfileDto
{
    public string? Username { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public AddressInputDto? Address { get; set; }
}

public sealed class AddressInputDto
{
    public int? ProvinceId { get; set; }
    public int? DistrictId { get; set; }
    public string? WardCode { get; set; }
    public string? StreetAddress { get; set; }
}

// ---------- SESSIONS ----------
// USER-03: GET /api/v1/me/sessions — no request body
// USER-04: DELETE /api/v1/me/sessions/{id} — path only, no body
// USER-05: POST /api/v1/auth/logout-all — no body (handled by AuthController)

// ---------- AVATAR ----------
// USER-06: POST /api/v1/me/avatar — multipart/form-data (IFormFile)
// USER-06B: GET /api/v1/me/avatar — no body

// ---------- REVIEWS ----------

/// <summary>REVIEW-01: POST /api/v1/reviews</summary>
public sealed class CreateReviewDto
{
    public string? TransactionType { get; set; } // "ORDER" | "AUCTION"
    public string? TransactionId { get; set; }
    public string? TargetUserId { get; set; }
    public int? Rating { get; set; }
    public string? Comment { get; set; }
    public List<string>? MediaUrls { get; set; }
}

// ---------- REPORTS ----------

/// <summary>REPORT-01: POST /api/v1/users/reports</summary>
public sealed class CreateUserReportDto
{
    public string? ReportedUserId { get; set; }
    public string? ViolationType { get; set; } // "SPAM" | "FRAUD" | "INAPPROPRIATE_CONTENT"
    public string? Description { get; set; }
    public List<string>? EvidenceUrls { get; set; }
}

// ---------- SELLER REGISTRATION ----------

/// <summary>SELLER-01: POST /api/v1/seller/registration</summary>
public sealed class CreateSellerRegistrationDto
{
    public string? ShopName { get; set; }
    public string? BusinessEmail { get; set; }
    public string? Hotline { get; set; }
    public AddressInputDto? WarehouseAddress { get; set; }
    public string? IdentityCardNumber { get; set; }
}

/// <summary>SELLER-02: POST /api/v1/seller/registration/otp/resend</summary>
public sealed class ResendSellerRegistrationOtpDto
{
    public string? RegistrationId { get; set; }
    public string? BusinessEmail { get; set; }
}

/// <summary>SELLER-04: POST /api/v1/seller/otp/verify</summary>
public sealed class VerifySellerOtpDto
{
    public string? RegistrationId { get; set; }
    public string? OtpCode { get; set; }
}

/// <summary>SELLER-07: PATCH /api/v1/seller/profile</summary>
public sealed class UpdateSellerOwnProfileDto
{
    public string? ShopName { get; set; }
    public string? Hotline { get; set; }
    public AddressInputDto? PickupAddress { get; set; }
    public string? Description { get; set; }
}

// ---------- ADMIN USER MANAGEMENT ----------

/// <summary>ADMIN-01B: POST /api/v1/admin/user</summary>
public sealed class CreateAdminUserDto
{
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public List<string>? RoleCodes { get; set; }
}

/// <summary>ADMIN-03: PUT /api/v1/admin/user/{userId}</summary>
public sealed class UpdateAdminUserDto
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Status { get; set; }
    public string? Reason { get; set; }
}

/// <summary>ADMIN-04: DELETE /api/v1/admin/user/{userId}</summary>
public sealed class DeleteAdminUserDto
{
    public string? Reason { get; set; }
}

/// <summary>ADMIN-05: PATCH /api/v1/admin/user/{userId}/ban</summary>
public sealed class BanAdminUserDto
{
    public string? Reason { get; set; }
    public int? BanDurationDays { get; set; } // 0 = permanent
}

/// <summary>ADMIN-06: PATCH /api/v1/admin/user/{userId}/unban</summary>
public sealed class UnbanAdminUserDto
{
    public string? Reason { get; set; }
}

/// <summary>ADMIN-10: PUT /api/v1/admin/user/{userId}/roles</summary>
public sealed class UpdateUserRolesDto
{
    public List<string>? RoleCodes { get; set; }
    public List<int>? RoleIds { get; set; }
    public string? Reason { get; set; }
}

/// <summary>ADMIN-14: PATCH /api/v1/admin/user/{userId}/reputation</summary>
public sealed class AdjustReputationDto
{
    public int? Delta { get; set; } // [-50..+50]
    public string? Reason { get; set; }
}

/// <summary>ADMIN-18: PUT /api/v1/admin/roles/{roleId}/permissions</summary>
public sealed class UpdateRolePermissionsDto
{
    public string? Description { get; set; }
    public List<int>? PermissionIds { get; set; }
}

/// <summary>ADMIN-13: PUT /api/v1/admin/configs</summary>
public sealed class UpdateSystemConfigDto
{
    public string? Key { get; set; }
    public string? Value { get; set; }
    public string? Reason { get; set; }
}

// ---------- ADMIN MODERATION ----------

/// <summary>REPORT-03: PUT /api/v1/admin/reports/{reportId}/resolve</summary>
public sealed class ResolveAdminReportDto
{
    public string? Action { get; set; } // "DEDUCT_REPUTATION" | "BAN_USER"
    public int? PointsDeducted { get; set; }
    public string? ResolutionNote { get; set; }
}

/// <summary>REPORT-04: PUT /api/v1/admin/reports/{reportId}/dismiss</summary>
public sealed class DismissAdminReportDto
{
    public string? Reason { get; set; }
}

/// <summary>REVIEW-04: PUT /api/v1/admin/reviews/{id}/hide</summary>
public sealed class HideAdminReviewDto
{
    public string? Reason { get; set; }
}

// ---------- ADMIN SELLER REGISTRATION ----------

/// <summary>ADMIN-08: PUT /api/v1/admin/seller-registrations/{registrationId}/approve</summary>
public sealed class ApproveSellerRegistrationDto
{
    public string? Note { get; set; }
}

/// <summary>ADMIN-09: PUT /api/v1/admin/seller-registrations/{registrationId}/reject</summary>
public sealed class RejectSellerRegistrationDto
{
    public string? RejectionReason { get; set; }
}

// ---------- UPLOAD FORM MODELS ----------

/// <summary>POST /api/v1/me/avatar — single file upload form</summary>
public sealed class UploadSingleFileForm
{
    public Microsoft.AspNetCore.Http.IFormFile? File { get; set; }
}

/// <summary>POST /api/v1/files/upload — multiple files upload form</summary>
public sealed class UploadMultipleFilesForm
{
    public List<Microsoft.AspNetCore.Http.IFormFile>? Files { get; set; }
}

/// <summary>POST /api/v1/me/seller/registration/documents — seller docs upload form</summary>
public sealed class UploadSellerDocumentsForm
{
    public string? RegistrationId { get; set; }
    public List<Microsoft.AspNetCore.Http.IFormFile>? Files { get; set; }
    public Microsoft.AspNetCore.Http.IFormFile? BusinessLicense { get; set; }
    public Microsoft.AspNetCore.Http.IFormFile? IdentityDocument { get; set; }
    public List<Microsoft.AspNetCore.Http.IFormFile>? AdditionalDocuments { get; set; }
}
