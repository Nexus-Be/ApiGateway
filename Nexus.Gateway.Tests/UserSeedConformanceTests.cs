namespace Nexus.Gateway.Tests;

using System;
using System.IO;
using System.Text.Json;
using Xunit;

/// <summary>
/// Conformance gate for the user seed fixtures: every <c>Seeds/users/{operation}.json</c> must
/// exist and be valid JSON with a valid object or array root structure.
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
    public void Seed_is_valid_json_structure(string operation)
    {
        var path = Path.Combine(SeedsDirectory(), $"{operation}.json");
        Assert.True(File.Exists(path), $"Missing seed fixture: {path}");

        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.ValueKind == JsonValueKind.Object || doc.RootElement.ValueKind == JsonValueKind.Array);
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
