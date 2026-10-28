namespace Nexus.Gateway.Dtos.User;

// ============================================================
// USER RESPONSE DTOs — aligned with flow_mock_excel.xlsx
// All types used by UsersController, AdminUsersController, AuthController
// ============================================================

// ---------- AUTH / COMMON ----------

/// <summary>AUTH-01 / USER-01: current logged-in user profile (used as ProducesResponseType)</summary>
public sealed class UserProfileDto
{
    public string? Id { get; set; }
    public string? UserId { get; set; }
    public string? Email { get; set; }
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Address { get; set; }
    public int ReputationScore { get; set; }
    public string? TrustLevel { get; set; }
    public string? Role { get; set; }
    public string[]? Roles { get; set; }
    public string[]? Permissions { get; set; }
    public bool IsSeller { get; set; }
    public string? Status { get; set; }
    public string? CreatedAt { get; set; }
}

/// <summary>AUTH-02/03: OTP operation response</summary>
public sealed class OtpResponseDto
{
    public string? Email { get; set; }
    public int OtpExpiresIn { get; set; }   // seconds
    public int ResendCooldown { get; set; } // seconds
}

// ---------- ME (UsersController) ----------

/// <summary>USER-02: PATCH /api/v1/me — profile updated response</summary>
public sealed class MyProfileUpdatedResponseDto
{
    public string? Id { get => UserId; set => UserId = value; }
    public string? UserId { get; set; }
    public string? Email { get; set; }
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Address { get; set; }
    public string? UpdatedAt { get; set; }
}

/// <summary>USER-03: POST /api/v1/me/avatar — avatar uploaded</summary>
public sealed class AvatarUploadedResponseDto
{
    public string? AvatarUrl { get; set; }
    public string? UpdatedAt { get; set; }
}

/// <summary>USER-03: GET /api/v1/me/avatar — avatar info</summary>
public sealed class AvatarResponseDto
{
    public string? UserId { get; set; }
    public string? AvatarUrl { get; set; }
    public string? UpdatedAt { get; set; }
}

/// <summary>USER-04: POST /api/v1/files/upload — multiple files uploaded</summary>
public sealed class UploadFilesResponseDto
{
    public List<UploadedFileItemDto>? Files { get; set; }
    public List<string>? FileUrls { get; set; }
}

public sealed class UploadedFileItemDto
{
    public string? FieldName { get; set; }
    public string? OriginalName { get; set; }
    public string? Url { get; set; }
    public long SizeBytes { get; set; }
    public string? MimeType { get; set; }
}

/// <summary>USER-05: GET /api/v1/me/sessions — list of active sessions</summary>
public sealed class SessionListResponseDto
{
    public List<SessionItemDto>? Items { get; set; }
    public UserPaginationDto? Pagination { get; set; }
}

public sealed class SessionItemDto
{
    public string? SessionId { get; set; }
    public string? DeviceInfo { get; set; }
    public string? IpAddress { get; set; }
    public string? LastActiveAt { get; set; }
    public bool IsCurrent { get; set; }
    public string? CreatedAt { get; set; }
}

/// <summary>USER-06: GET /api/v1/me/reputation</summary>
public sealed class MyReputationResponseDto
{
    public string? UserId { get; set; }
    public int ReputationScore { get; set; }
    public string? TrustLevel { get => ReputationLevel; set => ReputationLevel = value; }
    public string? ReputationLevel { get; set; }
    public bool CanBid { get; set; } = true;
    public bool CanSell { get; set; } = true;
    public int PositiveRatingCount { get => TotalPositive; set => TotalPositive = value; }
    public int NegativeRatingCount { get => TotalNegative; set => TotalNegative = value; }
    public int? NextLevelThreshold { get; set; }
    public int TotalPositive { get; set; }
    public int TotalNegative { get; set; }
}

/// <summary>USER-07: GET /api/v1/me/reputation/history</summary>
public sealed class ReputationHistoryResponseDto
{
    public List<ReputationHistoryItemDto>? Items { get; set; }
    public UserPaginationDto? Pagination { get; set; }
}

public sealed class ReputationHistoryItemDto
{
    public string? Id { get; set; }
    public int Delta { get; set; }
    public string? Reason { get; set; }
    public string? Source { get; set; }
    public string? CreatedAt { get; set; }
}

/// <summary>USER-08: GET /api/v1/me/penalties</summary>
public sealed class PenaltyListResponseDto
{
    public List<PenaltyItemDto>? Items { get; set; }
    public UserPaginationDto? Pagination { get; set; }
}

public sealed class PenaltyItemDto
{
    public string? Id { get => PenaltyId; set => PenaltyId = value; }
    public string? PenaltyId { get; set; }
    public string? ViolationType { get => Type; set => Type = value; }
    public string? Type { get; set; }
    public string? Reason { get; set; }
    public string? PenaltyApplied { get; set; }
    public int PointsDeducted { get; set; }
    public string? Status { get; set; }
    public string? AdminId { get; set; }
    public string? IssuedAt { get => CreatedAt; set => CreatedAt = value; }
    public string? ExpiresAt { get; set; }
    public string? CreatedAt { get; set; }
}

/// <summary>USER-09: GET /api/v1/me/reports — my submitted reports</summary>
public sealed class MyReportListResponseDto
{
    public List<UserReportItemDto>? Items { get; set; }
    public UserPaginationDto? Pagination { get; set; }
}

public sealed class UserReportItemDto
{
    public string? ReportId { get; set; }
    public string? TargetType { get; set; }
    public string? TargetId { get; set; }
    public string? Reason { get; set; }
    public string? Status { get; set; }
    public string? CreatedAt { get; set; }
}

/// <summary>USER-10: POST /api/v1/me/reports — report created</summary>
public sealed class UserReportCreatedResponseDto
{
    public string? ReportId { get; set; }
    public string? TargetType { get; set; }
    public string? TargetId { get; set; }
    public string? Reason { get; set; }
    public string? Status { get; set; }
    public string? SubmittedAt { get => CreatedAt; set => CreatedAt = value; }
    public string? CreatedAt { get; set; }
}

/// <summary>USER-11: GET /api/v1/users/{userId}/reviews — reviews received by seller/user</summary>
public sealed class UserReviewListResponseDto
{
    public List<UserReviewItemDto>? Items { get; set; }
    public UserPaginationDto? Pagination { get; set; }
    public ReviewSummaryDto? Summary { get; set; }
}

public sealed class UserReviewItemDto
{
    public string? Id { get => ReviewId; set => ReviewId = value; }
    public string? ReviewId { get; set; }
    public string? ReviewerId { get; set; }
    public string? ReviewerName { get => ReviewerDisplayName; set => ReviewerDisplayName = value; }
    public string? ReviewerDisplayName { get; set; }
    public string? ReviewerAvatarUrl { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public List<string>? MediaUrls { get; set; }
    public string? OrderId { get; set; }
    public bool IsHidden { get; set; }
    public string? CreatedAt { get; set; }
}

public sealed class ReviewSummaryDto
{
    public double AverageRating { get; set; }
    public int TotalReviews { get; set; }
}

/// <summary>USER-12: POST /api/v1/me/reviews — review created</summary>
public sealed class ReviewCreatedResponseDto
{
    public string? ReviewId { get; set; }
    public string? RevieweeId { get; set; }
    public string? OrderId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string? CreatedAt { get; set; }
}

/// <summary>USER-13: GET /api/v1/users/{userId} — public profile</summary>
public sealed class PublicUserProfileResponseDto
{
    public string? UserId { get; set; }
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public int ReputationScore { get; set; }
    public string? TrustLevel { get; set; }
    public bool IsSeller { get; set; }
    public string? CreatedAt { get; set; }
    public string? Id { get => UserId; set => UserId = value; }
    public string? Role { get; set; }
    public string? ReputationLevel { get => TrustLevel; set => TrustLevel = value; }
    public string? SellerStatus { get => IsSeller ? "Approved" : "None"; set => IsSeller = value == "Approved"; }
    public string? MemberSince { get => CreatedAt; set => CreatedAt = value; }
}

/// <summary>USER-14: GET /api/v1/users/{userId}/reputation</summary>
public sealed class UserReputationResponseDto
{
    public string? UserId { get; set; }
    public int ReputationScore { get; set; }
    public string? TrustLevel { get => ReputationLevel; set => ReputationLevel = value; }
    public string? ReputationLevel { get; set; }
    public List<string>? Badges { get; set; }
    public bool CanBid { get; set; } = true;
    public bool CanSell { get; set; } = true;
    public int TotalReviews { get; set; }
    public decimal PositiveRatio { get; set; }
    public int TotalPositive { get; set; }
    public int TotalNegative { get; set; }
}

// ---------- SELLER (UsersController) ----------

/// <summary>SELLER-01: GET /api/v1/me/seller/profile — seller own profile</summary>
public sealed class SellerOwnProfileResponseDto
{
    public string? SellerId { get; set; }
    public string? ShopName { get; set; }
    public string? BusinessEmail { get; set; }
    public string? Hotline { get; set; }
    public AddressInputDto? PickupAddress { get; set; }
    public decimal Rating { get; set; }
    public int TotalOrders { get; set; }
    public string? Status { get; set; }
    public string? JoinedAt { get; set; }

    // Backwards-compatible aliases
    public string? UserId { get => SellerId; set => SellerId = value; }
    public string? StoreName { get => ShopName; set => ShopName = value; }
    public string? ContactEmail { get => BusinessEmail; set => BusinessEmail = value; }
    public string? ContactPhone { get => Hotline; set => Hotline = value; }
    public string? StoreLogoUrl { get; set; }
    public string? StoreBannerUrl { get; set; }
    public string? StoreDescription { get; set; }
    public double AverageRating { get => (double)Rating; set => Rating = (decimal)value; }
    public int TotalReviews { get => TotalOrders; set => TotalOrders = value; }
    public string? CreatedAt { get => JoinedAt; set => JoinedAt = value; }
}

/// <summary>SELLER-02: PATCH /api/v1/me/seller/profile — seller profile updated</summary>
public sealed class SellerOwnProfileUpdatedResponseDto
{
    public string? SellerId { get; set; }
    public string? ShopName { get; set; }
    public string? Hotline { get; set; }
    public AddressInputDto? PickupAddress { get; set; }
    public string? UpdatedAt { get; set; }

    // Backwards-compatible aliases
    public string? UserId { get => SellerId; set => SellerId = value; }
    public string? StoreName { get => ShopName; set => ShopName = value; }
    public string? ContactPhone { get => Hotline; set => Hotline = value; }
    public string? StoreLogoUrl { get; set; }
    public string? StoreBannerUrl { get; set; }
    public string? ContactEmail { get; set; }
    public string? StoreDescription { get; set; }
}

/// <summary>SELLER-03: GET /api/v1/users/{userId}/seller — public seller profile</summary>
public sealed class SellerPublicProfileResponseDto
{
    public string? SellerId { get; set; }
    public string? ShopName { get; set; }
    public string? AvatarUrl { get; set; }
    public decimal Rating { get; set; }
    public int TotalProducts { get; set; }
    public string? JoinedAt { get; set; }

    // Backwards-compatible aliases
    public string? StoreName { get => ShopName; set => ShopName = value; }
    public string? StoreLogoUrl { get => AvatarUrl; set => AvatarUrl = value; }
    public double AverageRating { get => (double)Rating; set => Rating = (decimal)value; }
    public string? MemberSince { get => JoinedAt; set => JoinedAt = value; }
    public string? Status { get; set; }
    public int TotalReviews { get; set; }
    public string? StoreDescription { get; set; }
    public string? StoreBannerUrl { get; set; }
}

public sealed class SellerRegistrationDocumentDto
{
    public string? Id { get; set; }
    public string? FileName { get; set; }
    public string? FileUrl { get; set; }
    public string? FileType { get; set; }
    public string? UploadedAt { get; set; }
}

/// <summary>SELLER-04: GET /api/v1/me/seller/registration/status — registration status</summary>
public sealed class SellerRegistrationStatusResponseDto
{
    public string? RegistrationId { get; set; }
    public string? ShopName { get; set; }
    public string? BusinessEmail { get; set; }
    public string? Hotline { get; set; }
    public string? Status { get; set; }
    public string? RejectedReason { get; set; }
    public string? SubmittedAt { get; set; }
    public string? ReviewedAt { get; set; }
    public List<SellerRegistrationDocumentDto>? Documents { get; set; }

    // Backwards-compatible aliases
    public string? UserId { get; set; }
}

/// <summary>SELLER-05: POST /api/v1/me/seller/registration — registration submitted</summary>
public sealed class SellerRegistrationCreatedResponseDto
{
    public string? RegistrationId { get; set; }
    public string? BusinessEmail { get; set; }
    public string? Status { get; set; }
    public int OtpExpiresIn { get; set; }
    public string? CreatedAt { get; set; }
    public string? UserId { get; set; }
}

/// <summary>SELLER-06: POST /api/v1/me/seller/registration/otp/verify — OTP verified</summary>
public sealed class VerifySellerOtpResponseDto
{
    public string? RegistrationId { get; set; }
    public string? Status { get; set; }
    public string? VerifiedAt { get; set; }
    public bool IsVerified { get; set; } = true;
}

/// <summary>SELLER-07: POST /api/v1/me/seller/registration/otp/resend — OTP resent</summary>
public sealed class SellerRegistrationOtpResendResponseDto
{
    public string? RegistrationId { get; set; }
    public string? Email { get; set; }
    public int OtpExpiresIn { get; set; }
    public int ResendCooldown { get; set; }
}

// ---------- ADMIN USER RESPONSES (AdminUsersController) ----------

/// <summary>ADMIN-01: GET /api/v1/admin/users — paged user list</summary>
public sealed class AdminUserListResponseDto
{
    public List<AdminUserListItemDto>? Items { get; set; }
    public UserPaginationDto? Pagination { get; set; }
}

public sealed class AdminUserListItemDto
{
    public string? Id { get => UserId; set => UserId = value; }
    public string? UserId { get; set; }
    public string? Email { get; set; }
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Status { get; set; }
    public string[]? Roles { get; set; }
    public int ReputationScore { get; set; }
    public string? CreatedAt { get; set; }
    public string? LastLoginAt { get; set; }
}

/// <summary>ADMIN-02: GET /api/v1/admin/users/{userId} — full user detail</summary>
public sealed class AdminUserDetailResponseDto
{
    public string? Id { get => UserId; set => UserId = value; }
    public string? UserId { get; set; }
    public string? Email { get; set; }
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Status { get; set; }
    public string[]? Roles { get; set; }
    public string[]? Permissions { get; set; }
    public int ReputationScore { get; set; }
    public string? TrustLevel { get; set; }
    public object? SellerProfile { get; set; }
    public object? AuditHistory { get; set; }
    public bool IsBanned { get; set; }
    public string? BanReason { get; set; }
    public string? BannedUntil { get; set; }
    public string? CreatedAt { get; set; }
    public string? LastLoginAt { get; set; }
}

/// <summary>ADMIN-03: POST /api/v1/admin/users — admin creates user</summary>
public sealed class AdminUserCreatedResponseDto
{
    public string? UserId { get; set; }
    public string? Email { get; set; }
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public string[]? Roles { get; set; }
    public string? Status { get; set; }
    public string? CreatedAt { get; set; }
}

/// <summary>ADMIN-04: PUT /api/v1/admin/users/{userId} — admin updates user</summary>
public sealed class AdminUserUpdatedResponseDto
{
    public string? Id { get => UserId; set => UserId = value; }
    public string? UserId { get; set; }
    public string? Email { get; set; }
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Status { get; set; }
    public string? UpdatedBy { get; set; }
    public string? UpdatedAt { get; set; }
}

/// <summary>ADMIN-05: POST /api/v1/admin/users/{userId}/ban</summary>
public sealed class AdminUserBanResponseDto
{
    public string? Id { get => UserId; set => UserId = value; }
    public string? UserId { get; set; }
    public string? Status { get; set; }
    public string? Reason { get => BanReason; set => BanReason = value; }
    public string? BanReason { get; set; }
    public string? BannedUntil { get; set; }
    public string? BannedBy { get; set; }
    public string? BannedAt { get; set; }
}

/// <summary>ADMIN-06: POST /api/v1/admin/users/{userId}/unban</summary>
public sealed class AdminUserUnbanResponseDto
{
    public string? Id { get => UserId; set => UserId = value; }
    public string? UserId { get; set; }
    public string? Status { get; set; }
    public string? UnbannedBy { get; set; }
    public string? UnbannedAt { get; set; }
}

/// <summary>ADMIN-07: GET /api/v1/admin/users/{userId}/roles</summary>
public sealed class AdminUserRolesResponseDto
{
    public string? UserId { get; set; }
    public string[]? Roles { get; set; }
    public string[]? Permissions { get; set; }
}

/// <summary>ADMIN-08: POST /api/v1/admin/users/{userId}/reputation — adjust reputation</summary>
public sealed class AdminUserReputationResponseDto
{
    public string? UserId { get; set; }
    public int NewScore { get; set; }
    public int Delta { get; set; }
    public string? Reason { get; set; }
    public string? AdjustedBy { get; set; }
    public string? AdjustedAt { get; set; }
}

// ---------- ADMIN SELLER RESPONSES ----------

/// <summary>ADMIN-SELLER-01: GET /api/v1/admin/seller-registrations — paged list</summary>
public sealed class AdminSellerRegistrationListResponseDto
{
    public List<AdminSellerRegistrationItemDto>? Items { get; set; }
    public UserPaginationDto? Pagination { get; set; }
}

public sealed class AdminSellerRegistrationItemDto
{
    public string? RegistrationId { get; set; }
    public string? UserId { get; set; }
    public string? ShopName { get; set; }
    public string? BusinessEmail { get; set; }
    public string? Hotline { get; set; }
    public string? Status { get; set; }
    public string? SubmittedAt { get; set; }
    public string? ReviewedAt { get; set; }

    // Backwards-compatible aliases
    public string? UserEmail { get; set; }
    public string? StoreName { get => ShopName; set => ShopName = value; }
}

/// <summary>ADMIN-SELLER-02: GET /api/v1/admin/seller-registrations/{id} — detail</summary>
public sealed class AdminSellerRegistrationDetailResponseDto
{
    public string? RegistrationId { get; set; }
    public string? UserId { get; set; }
    public string? ShopName { get; set; }
    public string? BusinessEmail { get; set; }
    public string? Hotline { get; set; }
    public AddressInputDto? WarehouseAddress { get; set; }
    public string? IdentityCardNumber { get; set; }
    public string? Status { get; set; }
    public string? RejectedReason { get; set; }
    public string? SubmittedAt { get; set; }
    public string? ReviewedAt { get; set; }
    public string? ReviewedBy { get; set; }
    public List<SellerRegistrationDocumentDto>? Documents { get; set; }

    // Backwards-compatible aliases
    public string? UserEmail { get; set; }
    public string? StoreName { get => ShopName; set => ShopName = value; }
    public string? StoreDescription { get; set; }
    public string? ContactEmail { get => BusinessEmail; set => BusinessEmail = value; }
    public string? ContactPhone { get => Hotline; set => Hotline = value; }
}

/// <summary>ADMIN-SELLER-03: POST /api/v1/admin/seller-registrations/{id}/approve</summary>
public sealed class ApproveSellerRegistrationResponseDto
{
    public string? RegistrationId { get; set; }
    public string? UserId { get; set; }
    public string? Status { get; set; }
    public string? ApprovedBy { get; set; }
    public string? ApprovedAt { get; set; }
}

/// <summary>ADMIN-SELLER-04: POST /api/v1/admin/seller-registrations/{id}/reject</summary>
public sealed class RejectSellerRegistrationResponseDto
{
    public string? RegistrationId { get; set; }
    public string? UserId { get; set; }
    public string? Status { get; set; }
    public string? RejectedReason { get; set; }
    public string? RejectedBy { get; set; }
    public string? RejectedAt { get; set; }
}

/// <summary>ADMIN-SELLER-05: GET /api/v1/admin/sellers/{userId} — admin seller detail</summary>
public sealed class AdminSellerDetailResponseDto
{
    public string? SellerId { get => UserId; set => UserId = value; }
    public string? UserId { get; set; }
    public string? ShopName { get => StoreName; set => StoreName = value; }
    public string? BusinessEmail { get => ContactEmail; set => ContactEmail = value; }
    public string? Hotline { get => ContactPhone; set => ContactPhone = value; }
    public string? OwnerUserId { get => UserId; set => UserId = value; }
    public string? IdentityCardNumber { get; set; }
    public AddressInputDto? PickupAddress { get; set; }
    public decimal Rating { get => (decimal)AverageRating; set => AverageRating = (double)value; }
    public int TotalOrders { get; set; }
    public int TotalProducts { get; set; }
    public string? Status { get; set; }
    public string? JoinedAt { get => CreatedAt; set => CreatedAt = value; }
    public object? AuditHistory { get; set; }

    // Backwards-compatible aliases
    public string? StoreName { get; set; }
    public string? StoreDescription { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public double AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public string? CreatedAt { get; set; }
}

// ---------- ADMIN REPORT / REVIEW RESPONSES ----------

/// <summary>ADMIN-REPORT-01: GET /api/v1/admin/reports — paged list</summary>
public sealed class AdminReportListResponseDto
{
    public List<AdminReportItemDto>? Items { get; set; }
    public UserPaginationDto? Pagination { get; set; }
}

public sealed class AdminReportItemDto
{
    public string? Id { get => ReportId; set => ReportId = value; }
    public string? ReportId { get; set; }
    public string? ReporterId { get; set; }
    public string? ReporterEmail { get; set; }
    public string? ReportedUserId { get => TargetId; set => TargetId = value; }
    public string? TargetType { get; set; }
    public string? ViolationType { get => TargetType; set => TargetType = value; }
    public string? TargetId { get; set; }
    public string? Reason { get; set; }
    public string? Description { get => Reason; set => Reason = value; }
    public List<string>? EvidenceUrls { get; set; }
    public string? Status { get; set; }
    public string? CreatedAt { get; set; }
    public string? ResolvedAt { get; set; }
}

/// <summary>ADMIN-REPORT-02: POST /api/v1/admin/reports/{id}/resolve</summary>
public sealed class ResolveAdminReportResponseDto
{
    public string? ReportId { get; set; }
    public string? Status { get; set; }
    public string? Resolution { get; set; }
    public string? ResolvedBy { get; set; }
    public string? ResolvedAt { get; set; }
}

/// <summary>ADMIN-REPORT-03: POST /api/v1/admin/reports/{id}/dismiss</summary>
public sealed class DismissAdminReportResponseDto
{
    public string? ReportId { get; set; }
    public string? Status { get; set; }
    public string? DismissedBy { get; set; }
    public string? DismissedAt { get; set; }
}

/// <summary>ADMIN-REVIEW-01: POST /api/v1/admin/reviews/{id}/hide</summary>
public sealed class HideAdminReviewResponseDto
{
    public string? ReviewId { get; set; }
    public bool IsHidden { get; set; }
    public string? HiddenBy { get; set; }
    public string? HiddenAt { get; set; }
}

// ---------- ADMIN ROLES / PERMISSIONS RESPONSES ----------

/// <summary>ADMIN-ROLE-01: GET /api/v1/admin/roles</summary>
public sealed class AdminRoleListResponseDto
{
    public List<AdminRoleItemDto>? Items { get; set; }
}

public sealed class AdminRoleItemDto
{
    public string? RoleId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string[]? Permissions { get; set; }
}

/// <summary>ADMIN-ROLE-02: GET /api/v1/admin/permissions</summary>
public sealed class AdminPermissionListResponseDto
{
    public List<AdminPermissionItemDto>? Items { get; set; }
}

public sealed class AdminPermissionItemDto
{
    public string? PermissionId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Module { get; set; }
}

/// <summary>ADMIN-ROLE-03: PUT /api/v1/admin/roles/{roleId}/permissions</summary>
public sealed class AdminRolePermissionsUpdatedResponseDto
{
    public string? RoleId { get; set; }
    public string? RoleName { get; set; }
    public string[]? Permissions { get; set; }
    public string? UpdatedBy { get; set; }
    public string? UpdatedAt { get; set; }
}

// ---------- ADMIN CONFIG / AUDIT RESPONSES ----------

/// <summary>ADMIN-CONFIG-01: GET /api/v1/admin/configs</summary>
public sealed class AdminConfigListResponseDto
{
    public List<AdminConfigItemDto>? Items { get; set; }
}

public sealed class AdminConfigItemDto
{
    public string? ConfigKey { get; set; }
    public string? ConfigValue { get; set; }
    public string? Description { get; set; }
    public string? UpdatedBy { get; set; }
    public string? UpdatedAt { get; set; }
}

/// <summary>ADMIN-CONFIG-02: PUT /api/v1/admin/configs/{key}</summary>
public sealed class AdminConfigUpdatedResponseDto
{
    public string? ConfigKey { get; set; }
    public string? ConfigValue { get; set; }
    public string? UpdatedBy { get; set; }
    public string? UpdatedAt { get; set; }
}

/// <summary>ADMIN-AUDIT-01: GET /api/v1/admin/audit-logs</summary>
public sealed class AdminAuditLogListResponseDto
{
    public List<AdminAuditLogItemDto>? Items { get; set; }
    public UserPaginationDto? Pagination { get; set; }
}

public sealed class AdminAuditLogItemDto
{
    public string? LogId { get; set; }
    public string? AdminId { get; set; }
    public string? AdminEmail { get; set; }
    public string? Action { get; set; }
    public string? TargetType { get; set; }
    public string? TargetId { get; set; }
    public object? Metadata { get; set; }
    public string? IpAddress { get; set; }
    public string? CreatedAt { get; set; }
}

// ---------- ROLE ASSIGNMENT ----------

/// <summary>Controller request: roles assignment by Admin</summary>
public sealed class AssignAdminUserRolesDto
{
    public string[]? RoleCodes { get; set; }
    public string[]? Roles { get => RoleCodes; set => RoleCodes = value; }
    public string? Reason { get; set; }
}

/// <summary>Controller request: adjust admin user reputation</summary>
public sealed class AdjustAdminUserReputationDto
{
    public int Delta { get; set; }
    public string? Reason { get; set; }
}

/// <summary>Controller request: update admin config</summary>
public sealed class UpdateAdminConfigDto
{
    public string? Key { get; set; }
    public string? Value { get; set; }
    public string? ConfigValue { get; set; }
    public string? Description { get; set; }
    public string? Reason { get; set; }
}

/// <summary>Controller request: update role permissions</summary>
public sealed class UpdateAdminRolePermissionsDto
{
    public string[]? Permissions { get; set; }
}

// ---------- SHARED PAGINATION ----------

public sealed class UserPaginationDto
{
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasPreviousPage { get; set; }
    public bool HasNextPage { get; set; }
}
