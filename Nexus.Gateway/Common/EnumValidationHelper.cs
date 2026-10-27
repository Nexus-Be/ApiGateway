namespace Nexus.Gateway.Common;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Reusable helper to validate string values against domain enum contracts in a case-insensitive manner.
/// Throws <see cref="GatewayInputException"/> with error code VAL_INVALID_INPUT when invalid.
/// </summary>
public static class EnumValidationHelper
{
    public const string ErrorCode = "VAL_INVALID_INPUT";

    public static void EnsureEnum<TEnum>(string? value, string field) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        var names = Enum.GetNames<TEnum>();
        if (!names.Any(name => MatchesEnum(value, name)))
        {
            var allowed = string.Join(", ", names);
            throw new GatewayInputException($"{field} must be one of: {allowed}.", field, ErrorCode);
        }
    }

    public static void EnsureOneOf(string? value, string field, IReadOnlyList<string> allowedValues)
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        if (!allowedValues.Any(v => MatchesEnum(value, v)))
        {
            var allowed = string.Join(", ", allowedValues);
            throw new GatewayInputException($"{field} must be one of: {allowed}.", field, ErrorCode);
        }
    }

    public static bool MatchesEnum(string input, string target)
    {
        if (string.Equals(input, target, StringComparison.OrdinalIgnoreCase))
            return true;

        var normalizedInput = input.Replace("_", string.Empty);
        var normalizedTarget = target.Replace("_", string.Empty);
        return string.Equals(normalizedInput, normalizedTarget, StringComparison.OrdinalIgnoreCase);
    }
}
