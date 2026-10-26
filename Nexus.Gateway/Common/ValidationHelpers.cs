namespace Nexus.Gateway.Common;

using Microsoft.AspNetCore.Http;
using Nexus.Gateway.Responses;

public static class ValidationHelpers
{
    public const string DefaultCurrency = "VND";
    public const string PageMessage = "Page must be >= 1 and pageSize must be between 1 and 100.";
    public static readonly string[] KnownRoles = ["buyer", "seller", "staff", "admin"];
    public const int MaxCheckoutLines = 50;
    public const int MaxCheckoutQuantity = 1000;

    public static IResult Bad(HttpContext context, string message, string field = "General", string errorCode = "VAL_INVALID_INPUT") =>
        ApiResponseFactory.BadRequest(context, message, field, errorCode);

    public static bool PageOk(int page, int size) => ApiResponseFactory.PageOk(page, size);
    public static bool GuidOk(string? value) => ApiResponseFactory.GuidOk(value);

    public static void EnsureGuid(string? value, string field = "Id")
    {
        if (!GuidOk(value))
            throw new GatewayInputException($"{field} must be a valid UUID.", field);
    }

    public static void EnsureRequired(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new GatewayInputException($"{field} is required.", field);
    }
}
