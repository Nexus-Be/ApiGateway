namespace Nexus.Gateway.Common;

using Microsoft.AspNetCore.Http;
using Nexus.Gateway.Responses;

public static class ValidationHelpers
{
    public const string DefaultCurrency = "VND";
    public const string PageMessage = "page >= 1 và pageSize trong khoảng 1..100";
    public static readonly string[] KnownRoles = ["buyer", "seller", "staff", "admin"];
    public const int MaxCheckoutLines = 50;
    public const int MaxCheckoutQuantity = 1000;

    public static IResult Bad(HttpContext context, string message, string field = "General", string errorCode = "VAL_INVALID_INPUT") =>
        ApiResponseFactory.BadRequest(context, message, field, errorCode);

    public static bool PageOk(int page, int size) => ApiResponseFactory.PageOk(page, size);
    public static bool GuidOk(string? value) => ApiResponseFactory.GuidOk(value);
}
