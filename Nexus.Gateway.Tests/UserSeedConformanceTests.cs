namespace Nexus.Gateway.Tests;

using System.Text.Json;
using Google.Protobuf;
using ShoppingCart.Contracts.V1;
using Xunit;

/// <summary>
/// Conformance gate for the user seed fixtures: every <c>Seeds/users/{operation}.json</c> must
/// deserialize into the exact contract message the controller returns for that operation.
/// </summary>
public sealed class UserSeedConformanceTests
{
    [Theory]
    [InlineData("refresh")]
    [InlineData("password-forgot")]
    [InlineData("password-reset")]
    [InlineData("password-change")]
    [InlineData("logout")]
    [InlineData("logout-all")]
    [InlineData("user-sessions")]
    [InlineData("user-session-revoke")]
    [InlineData("user-profile-update")]
    [InlineData("user-my-avatar")]
    [InlineData("user-my-avatar-upload")]
    [InlineData("user-my-reputation")]
    [InlineData("user-my-penalties")]
    [InlineData("user-public-profile")]
    [InlineData("user-avatar")]
    [InlineData("seller-public-profile")]
    [InlineData("user-reputation")]
    [InlineData("user-reputation-history")]
    [InlineData("user-reviews")]
    [InlineData("review-files-upload")]
    [InlineData("review-create")]
    [InlineData("report-files-upload")]
    [InlineData("user-report-create")]
    [InlineData("user-my-reports")]
    [InlineData("seller-registration-create")]
    [InlineData("seller-registration-otp-resend")]
    [InlineData("seller-registration-documents")]
    [InlineData("seller-otp-verify")]
    [InlineData("seller-registration-status")]
    [InlineData("seller-own-profile")]
    [InlineData("seller-own-profile-update")]
    [InlineData("admin-seller-registrations")]
    [InlineData("admin-seller-registration-detail")]
    [InlineData("admin-seller-registration-approve")]
    [InlineData("admin-seller-registration-reject")]
    [InlineData("admin-reports")]
    [InlineData("admin-report-resolve")]
    [InlineData("admin-report-dismiss")]
    [InlineData("admin-review-hide")]
    [InlineData("admin-users")]
    [InlineData("admin-user-create")]
    [InlineData("admin-user-detail")]
    [InlineData("admin-user-update")]
    [InlineData("admin-user-delete")]
    [InlineData("admin-user-ban")]
    [InlineData("admin-user-unban")]
    [InlineData("admin-seller-detail")]
    [InlineData("admin-user-roles-assign")]
    [InlineData("admin-user-reputation-adjust")]
    [InlineData("admin-roles")]
    [InlineData("admin-permissions")]
    [InlineData("admin-role-permissions-update")]
    [InlineData("admin-audit-logs")]
    [InlineData("admin-configs")]
    [InlineData("admin-config-update")]
    public void Seed_matches_its_contract_message(string operation)
    {
        var path = Path.Combine(SeedsDirectory(), $"{operation}.json");
        Assert.True(File.Exists(path), $"Missing seed fixture: {path}");

        var json = File.ReadAllText(path);
        var message = Parse(operation, json);

        Assert.False(string.IsNullOrEmpty(message.GetType().Name));

        var canonical = JsonFormatter.Default.Format(message);
        _ = Parse(operation, canonical);
    }

    private static IMessage Parse(string operation, string json)
    {
        switch (operation)
        {
            case "refresh":
                return RefreshTokenResponse.Parser.ParseJson(json);
            case "password-forgot":
                return ForgotPasswordResponse.Parser.ParseJson(json);
            case "password-reset":
                return ResetPasswordResponse.Parser.ParseJson(json);
            case "password-change":
                return ChangePasswordResponse.Parser.ParseJson(json);
            case "logout":
                return LogoutResponse.Parser.ParseJson(json);
            case "logout-all":
                return LogoutAllResponse.Parser.ParseJson(json);

            case "user-sessions":
                return SessionListResponse.Parser.ParseJson(json);
            case "user-session-revoke":
                return RevokeSessionResponse.Parser.ParseJson(json);
            case "user-profile-update":
                return MyProfileUpdatedResponse.Parser.ParseJson(json);
            case "user-my-avatar":
            case "user-avatar":
                return AvatarResponse.Parser.ParseJson(json);
            case "user-my-avatar-upload":
                return AvatarUploadedResponse.Parser.ParseJson(json);
            case "user-my-reputation":
                return MyReputationResponse.Parser.ParseJson(json);
            case "user-my-penalties":
                return PenaltyListResponse.Parser.ParseJson(json);
            case "user-public-profile":
                return PublicUserProfileResponse.Parser.ParseJson(json);
            case "seller-public-profile":
                return SellerPublicProfileResponse.Parser.ParseJson(json);
            case "user-reputation":
                return UserReputationResponse.Parser.ParseJson(json);
            case "user-reputation-history":
                return ReputationHistoryResponse.Parser.ParseJson(json);
            case "user-reviews":
                return UserReviewListResponse.Parser.ParseJson(json);
            case "review-files-upload":
            case "report-files-upload":
            case "seller-registration-documents":
                return UploadFilesResponse.Parser.ParseJson(json);
            case "review-create":
                return ReviewCreatedResponse.Parser.ParseJson(json);
            case "user-report-create":
                return UserReportCreatedResponse.Parser.ParseJson(json);
            case "user-my-reports":
                return MyReportListResponse.Parser.ParseJson(json);
            case "seller-registration-create":
                return SellerRegistrationCreatedResponse.Parser.ParseJson(json);
            case "seller-registration-otp-resend":
                return SellerRegistrationOtpResendResponse.Parser.ParseJson(json);
            case "seller-otp-verify":
                return VerifySellerOtpResponse.Parser.ParseJson(json);
            case "seller-registration-status":
                return SellerRegistrationStatusResponse.Parser.ParseJson(json);
            case "seller-own-profile":
                return SellerOwnProfileResponse.Parser.ParseJson(json);
            case "seller-own-profile-update":
                return SellerOwnProfileUpdatedResponse.Parser.ParseJson(json);

            case "admin-seller-registrations":
                return AdminSellerRegistrationListResponse.Parser.ParseJson(json);
            case "admin-seller-registration-detail":
                return AdminSellerRegistrationDetailResponse.Parser.ParseJson(json);
            case "admin-seller-registration-approve":
                return ApproveSellerRegistrationResponse.Parser.ParseJson(json);
            case "admin-seller-registration-reject":
                return RejectSellerRegistrationResponse.Parser.ParseJson(json);
            case "admin-reports":
                return AdminReportListResponse.Parser.ParseJson(json);
            case "admin-report-resolve":
                return ResolveAdminReportResponse.Parser.ParseJson(json);
            case "admin-report-dismiss":
                return DismissAdminReportResponse.Parser.ParseJson(json);
            case "admin-review-hide":
                return HideAdminReviewResponse.Parser.ParseJson(json);
            case "admin-users":
                return AdminUserListResponse.Parser.ParseJson(json);
            case "admin-user-create":
                return AdminUserCreatedResponse.Parser.ParseJson(json);
            case "admin-user-detail":
                return AdminUserDetailResponse.Parser.ParseJson(json);
            case "admin-user-update":
                return AdminUserUpdatedResponse.Parser.ParseJson(json);
            case "admin-user-delete":
                return AdminUserDeletedResponse.Parser.ParseJson(json);
            case "admin-user-ban":
                return AdminUserBanResponse.Parser.ParseJson(json);
            case "admin-user-unban":
                return AdminUserUnbanResponse.Parser.ParseJson(json);
            case "admin-seller-detail":
                return AdminSellerDetailResponse.Parser.ParseJson(json);
            case "admin-user-roles-assign":
                return AdminUserRolesResponse.Parser.ParseJson(json);
            case "admin-user-reputation-adjust":
                return AdminUserReputationResponse.Parser.ParseJson(json);
            case "admin-roles":
                return AdminRoleListResponse.Parser.ParseJson(json);
            case "admin-permissions":
                return AdminPermissionListResponse.Parser.ParseJson(json);
            case "admin-role-permissions-update":
                return AdminRolePermissionsUpdatedResponse.Parser.ParseJson(json);
            case "admin-audit-logs":
                return AdminAuditLogListResponse.Parser.ParseJson(json);
            case "admin-configs":
                return AdminConfigListResponse.Parser.ParseJson(json);
            case "admin-config-update":
                return AdminConfigUpdatedResponse.Parser.ParseJson(json);

            default:
                throw new Xunit.Sdk.XunitException($"No contract message mapped for seed '{operation}'.");
        }
    }

    private static string SeedsDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Nexus.Gateway", "Seeds", "users");
            if (Directory.Exists(candidate)) return candidate;

            candidate = Path.Combine(directory.FullName, "Seeds", "users");
            if (Directory.Exists(candidate)) return candidate;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Nexus.Gateway/Seeds/users from the test output directory.");
    }
}
