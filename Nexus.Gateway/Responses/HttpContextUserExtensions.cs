namespace Nexus.Gateway.Responses;

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ShoppingCart.Contracts.V1;

public static class HttpContextUserExtensions
{
    public static UserRequest ToUserRequest(this HttpContext context) =>
        new() { UserId = context.User.FindFirst("sub")?.Value ?? "" };

    public static string[] GetRoles(this HttpContext context) =>
        context.User.FindAll("role").Select(x => x.Value).ToArray();

    public static string GetPrimaryRole(this HttpContext context)
    {
        var roles = context.GetRoles();
        foreach (var role in new[] { "admin", "staff", "seller" })
        {
            if (roles.Contains(role)) return role;
        }
        return roles.FirstOrDefault() ?? "buyer";
    }
}
