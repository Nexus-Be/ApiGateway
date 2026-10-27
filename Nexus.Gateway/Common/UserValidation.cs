namespace Nexus.Gateway.Common;

using System;
using System.Linq;
using System.Text.RegularExpressions;
using Nexus.Gateway.Dtos.User;

public static class UserValidation
{
    public const string ErrorCode = "VAL_INVALID_INPUT";
    public const int MaxPageSize = 100;

    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static readonly string[] AllowedUserStatuses = ["ACTIVE", "INACTIVE", "BANNED", "SUSPENDED", "PENDING_VERIFICATION"];
    public static readonly string[] AllowedViolationTypes = ["SPAM", "HARASSMENT", "FRAUD", "COUNTERFEIT", "INAPPROPRIATE_CONTENT", "OTHER"];
    public static readonly string[] AllowedReportStatuses = ["PENDING", "RESOLVED", "DISMISSED"];
    public static readonly string[] AllowedSellerRegistrationStatuses = ["PENDING", "APPROVED", "REJECTED"];

    public static string EnsureRequired(string? value, string field, string? label = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new GatewayInputException($"{label ?? field} is required.", field, ErrorCode);
        }
        return value.Trim();
    }

    public static string EnsureEmail(string? email, string field = "Email")
    {
        var trimmed = EnsureRequired(email, field, "Email address");
        if (!EmailRegex.IsMatch(trimmed))
        {
            throw new GatewayInputException("Email address is invalid.", field, ErrorCode);
        }
        return trimmed;
    }

    public static string EnsurePassword(string? password, string field = "Password")
    {
        var trimmed = EnsureRequired(password, field, "Password");
        if (trimmed.Length < 8)
        {
            throw new GatewayInputException("Password must be at least 8 characters long.", field, ErrorCode);
        }
        return trimmed;
    }

    public static string EnsureOtp(string? otp, string field = "OtpCode")
    {
        var trimmed = EnsureRequired(otp, field, "OTP code");
        if (trimmed.Length != 6 || !trimmed.All(char.IsDigit))
        {
            throw new GatewayInputException("OTP code must be exactly 6 digits.", field, ErrorCode);
        }
        return trimmed;
    }

    public static Guid EnsureUuid(string? id, string field = "Id")
    {
        var trimmed = EnsureRequired(id, field);
        if (!Guid.TryParse(trimmed, out var guid))
        {
            throw new GatewayInputException($"{field} must be a valid UUID.", field, ErrorCode);
        }
        return guid;
    }

    public static void EnsurePaged(int page, int pageSize)
    {
        if (page < 1 || pageSize < 1 || pageSize > MaxPageSize)
        {
            throw new GatewayInputException(
                $"Page must be at least 1 and pageSize must be between 1 and {MaxPageSize}.",
                "Pagination",
                ErrorCode);
        }
    }

    public static void EnsureRating(int? rating, string field = "Rating")
    {
        if (!rating.HasValue || rating < 1 || rating > 5)
        {
            throw new GatewayInputException("Rating must be an integer between 1 and 5.", field, ErrorCode);
        }
    }
}
