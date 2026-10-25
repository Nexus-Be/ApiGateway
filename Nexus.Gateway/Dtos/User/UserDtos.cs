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
