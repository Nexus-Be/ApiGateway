namespace Nexus.Gateway.Dtos.User;

using Microsoft.AspNetCore.Http;

public sealed class RefreshTokenDto
{
    public string? RefreshToken { get; set; }
    public string? DeviceInfo { get; set; }
}

public sealed class ForgotPasswordDto
{
    public string? Email { get; set; }
}

public sealed class ResetPasswordDto
{
    public string? Email { get; set; }
    public string? OtpCode { get; set; }
    public string? NewPassword { get; set; }
    public string? ConfirmPassword { get; set; }
}

public sealed class ChangePasswordDto
{
    public string? OldPassword { get; set; }
    public string? NewPassword { get; set; }
    public string? ConfirmPassword { get; set; }
}

public sealed class LogoutDto
{
    public string? RefreshToken { get; set; }
}

public sealed class AddressInputDto
{
    public int? ProvinceId { get; set; }
    public int? DistrictId { get; set; }
    public string? WardCode { get; set; }
    public string? StreetAddress { get; set; }
}

public sealed class UpdateMyProfileDto
{
    public string? Username { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public AddressInputDto? Address { get; set; }
    public int? ProvinceId { get; set; }
    public int? DistrictId { get; set; }
    public string? WardCode { get; set; }
    public string? StreetAddress { get; set; }
}

public sealed class CreateReviewDto
{
    public string? TransactionType { get; set; }
    public string? TransactionId { get; set; }
    public string? TargetUserId { get; set; }
    public int? Rating { get; set; }
    public string? Comment { get; set; }
    public List<string>? MediaUrls { get; set; }
}

public sealed class CreateUserReportDto
{
    public string? ReportedUserId { get; set; }
    public string? ViolationType { get; set; }
    public string? Description { get; set; }
    public List<string>? EvidenceUrls { get; set; }
}

public sealed class CreateSellerRegistrationDto
{
    public string? ShopName { get; set; }
    public string? BusinessEmail { get; set; }
    public string? Hotline { get; set; }
    public AddressInputDto? WarehouseAddress { get; set; }
    public int? ProvinceId { get; set; }
    public int? DistrictId { get; set; }
    public string? WardCode { get; set; }
    public string? StreetAddress { get; set; }
    public string? IdentityCardNumber { get; set; }
}

public sealed class ResendSellerRegistrationOtpDto
{
    public string? RegistrationId { get; set; }
    public string? BusinessEmail { get; set; }
}

public sealed class VerifySellerOtpDto
{
    public string? RegistrationId { get; set; }
    public string? OtpCode { get; set; }
}

public sealed class UpdateSellerOwnProfileDto
{
    public string? ShopName { get; set; }
    public string? Hotline { get; set; }
    public AddressInputDto? PickupAddress { get; set; }
    public int? ProvinceId { get; set; }
    public int? DistrictId { get; set; }
    public string? WardCode { get; set; }
    public string? StreetAddress { get; set; }
    public string? Description { get; set; }
}

public sealed class ApproveSellerRegistrationDto
{
    public string? Note { get; set; }
}

public sealed class RejectSellerRegistrationDto
{
    public string? RejectionReason { get; set; }
}

public sealed class ResolveAdminReportDto
{
    public string? Action { get; set; }
    public int? PointsDeducted { get; set; }
    public string? ResolutionNote { get; set; }
}

public sealed class DismissAdminReportDto
{
    public string? Reason { get; set; }
}

public sealed class HideAdminReviewDto
{
    public string? Reason { get; set; }
}

public sealed class CreateAdminUserDto
{
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public List<string>? RoleCodes { get; set; }
}

public sealed class UpdateAdminUserDto
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Status { get; set; }
    public string? Reason { get; set; }
}

public sealed class DeleteAdminUserDto
{
    public string? Reason { get; set; }
}

public sealed class BanAdminUserDto
{
    public string? Reason { get; set; }
    public int? BanDurationDays { get; set; }
}

public sealed class UnbanAdminUserDto
{
    public string? Reason { get; set; }
}

public sealed class AssignAdminUserRolesDto
{
    public List<string>? RoleCodes { get; set; }
    public List<int>? RoleIds { get; set; }
    public string? Reason { get; set; }
}

public sealed class AdjustAdminUserReputationDto
{
    public int? Delta { get; set; }
    public string? Reason { get; set; }
}

public sealed class UpdateAdminRolePermissionsDto
{
    public string? Description { get; set; }
    public List<int>? PermissionIds { get; set; }
}

public sealed class UpdateAdminConfigDto
{
    public string? Key { get; set; }
    public string? Value { get; set; }
    public string? Reason { get; set; }
}

public sealed class UploadSingleFileForm
{
    public IFormFile? File { get; set; }
}

public sealed class UploadMultipleFilesForm
{
    public List<IFormFile>? Files { get; set; }
}

public sealed class UploadSellerDocumentsForm
{
    public string? RegistrationId { get; set; }
    public List<IFormFile>? Files { get; set; }
}

// ============================================================================
// Response DTOs for Gateway Swagger / Documentation Contracts
// ============================================================================

public sealed class UserProfileDto
{
    public string? UserId { get; set; }
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public List<string>? Roles { get; set; }
    public string? Status { get; set; }
}

public sealed class OtpResponseDto
{
    public string? Message { get; set; }
    public int ExpiresInSeconds { get; set; }
}

public sealed class LoginResponseDto
{
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public int ExpiresIn { get; set; }
    public UserProfileDto? User { get; set; }
}

public sealed class RefreshTokenResponseDto
{
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public int ExpiresIn { get; set; }
}

public sealed class ForgotPasswordResponseDto
{
    public string? Message { get; set; }
}

public sealed class ResetPasswordResponseDto
{
    public string? Message { get; set; }
}

public sealed class ChangePasswordResponseDto
{
    public string? Message { get; set; }
}

public sealed class LogoutResponseDto
{
    public string? Message { get; set; }
}

public sealed class LogoutAllResponseDto
{
    public string? Message { get; set; }
}

public sealed class SessionListResponseDto
{
    public List<object>? Sessions { get; set; }
}

public sealed class MyProfileUpdatedResponseDto
{
    public string? Message { get; set; }
    public UserProfileDto? Profile { get; set; }
}

public sealed class AvatarResponseDto
{
    public string? AvatarUrl { get; set; }
}

public sealed class AvatarUploadedResponseDto
{
    public string? AvatarUrl { get; set; }
}

public sealed class MyReputationResponseDto
{
    public int Score { get; set; }
    public string? Rank { get; set; }
}

public sealed class PenaltyListResponseDto
{
    public List<object>? Penalties { get; set; }
}

public sealed class PublicUserProfileResponseDto
{
    public string? UserId { get; set; }
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public int ReputationScore { get; set; }
}

public sealed class SellerPublicProfileResponseDto
{
    public string? SellerId { get; set; }
    public string? ShopName { get; set; }
    public string? Description { get; set; }
    public string? AvatarUrl { get; set; }
    public int ReputationScore { get; set; }
}

public sealed class UserReputationResponseDto
{
    public int Score { get; set; }
    public string? Rank { get; set; }
}

public sealed class ReputationHistoryResponseDto
{
    public List<object>? History { get; set; }
}

public sealed class UserReviewListResponseDto
{
    public List<object>? Reviews { get; set; }
}

public sealed class UploadFilesResponseDto
{
    public List<string>? FileUrls { get; set; }
}

public sealed class ReviewCreatedResponseDto
{
    public string? ReviewId { get; set; }
    public string? Message { get; set; }
}

public sealed class UserReportCreatedResponseDto
{
    public string? ReportId { get; set; }
    public string? Message { get; set; }
}

public sealed class MyReportListResponseDto
{
    public List<object>? Reports { get; set; }
}

public sealed class SellerRegistrationCreatedResponseDto
{
    public string? RegistrationId { get; set; }
    public string? Status { get; set; }
}

public sealed class SellerRegistrationOtpResendResponseDto
{
    public string? Message { get; set; }
}

public sealed class VerifySellerOtpResponseDto
{
    public string? RegistrationId { get; set; }
    public string? Status { get; set; }
    public string? Message { get; set; }
}

public sealed class SellerRegistrationStatusResponseDto
{
    public string? RegistrationId { get; set; }
    public string? Status { get; set; }
    public string? Reason { get; set; }
}

public sealed class SellerOwnProfileResponseDto
{
    public string? SellerId { get; set; }
    public string? ShopName { get; set; }
    public string? Hotline { get; set; }
    public string? Description { get; set; }
}

public sealed class SellerOwnProfileUpdatedResponseDto
{
    public string? Message { get; set; }
}

public sealed class AdminSellerRegistrationListResponseDto
{
    public List<object>? Registrations { get; set; }
}

public sealed class AdminSellerRegistrationDetailResponseDto
{
    public string? RegistrationId { get; set; }
    public string? ShopName { get; set; }
    public string? BusinessEmail { get; set; }
    public string? Status { get; set; }
}

public sealed class ApproveSellerRegistrationResponseDto
{
    public string? RegistrationId { get; set; }
    public string? Status { get; set; }
}

public sealed class RejectSellerRegistrationResponseDto
{
    public string? RegistrationId { get; set; }
    public string? Status { get; set; }
}

public sealed class AdminReportListResponseDto
{
    public List<object>? Reports { get; set; }
}

public sealed class ResolveAdminReportResponseDto
{
    public string? ReportId { get; set; }
    public string? Status { get; set; }
}

public sealed class DismissAdminReportResponseDto
{
    public string? ReportId { get; set; }
    public string? Status { get; set; }
}

public sealed class HideAdminReviewResponseDto
{
    public string? ReviewId { get; set; }
    public bool IsHidden { get; set; }
}

public sealed class AdminUserListResponseDto
{
    public List<UserProfileDto>? Users { get; set; }
}

public sealed class AdminUserCreatedResponseDto
{
    public string? UserId { get; set; }
    public string? Email { get; set; }
}

public sealed class AdminUserDetailResponseDto
{
    public UserProfileDto? User { get; set; }
}

public sealed class AdminUserUpdatedResponseDto
{
    public string? Message { get; set; }
}

public sealed class AdminUserBanResponseDto
{
    public string? UserId { get; set; }
    public string? Status { get; set; }
}

public sealed class AdminUserUnbanResponseDto
{
    public string? UserId { get; set; }
    public string? Status { get; set; }
}

public sealed class AdminSellerDetailResponseDto
{
    public string? SellerId { get; set; }
    public string? ShopName { get; set; }
}

public sealed class AdminUserRolesResponseDto
{
    public string? UserId { get; set; }
    public List<string>? Roles { get; set; }
}

public sealed class AdminUserReputationResponseDto
{
    public string? UserId { get; set; }
    public int ReputationScore { get; set; }
}

public sealed class AdminRoleListResponseDto
{
    public List<object>? Roles { get; set; }
}

public sealed class AdminPermissionListResponseDto
{
    public List<object>? Permissions { get; set; }
}

public sealed class AdminRolePermissionsUpdatedResponseDto
{
    public string? Message { get; set; }
}

public sealed class AdminAuditLogListResponseDto
{
    public List<object>? AuditLogs { get; set; }
}

public sealed class AdminConfigListResponseDto
{
    public List<object>? Configs { get; set; }
}

public sealed class AdminConfigUpdatedResponseDto
{
    public string? Key { get; set; }
    public string? Value { get; set; }
}
