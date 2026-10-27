using Microsoft.AspNetCore.Http;

namespace Nexus.Gateway.Security;

public static class GatewayPolicies
{
    public const string Authenticated = "authenticated";
    public const string Customer = "customer";
    public const string Seller = "seller";
    public const string Staff = "staff";
    public const string Admin = "admin";
    public const string GatewayAdmin = "gateway-admin";
    public const string StaffOrAdmin = "staff-admin";
    public const string SellerOrStaffOrAdmin = "seller-staff-admin";

    public static bool IsMockPolicy(string? policy) =>
        string.IsNullOrWhiteSpace(policy) ||
        policy.Equals("public", StringComparison.OrdinalIgnoreCase) ||
        policy.Equals("anonymous", StringComparison.OrdinalIgnoreCase) ||
        policy.Equals(Authenticated, StringComparison.OrdinalIgnoreCase) ||
        policy.Equals(Customer, StringComparison.OrdinalIgnoreCase) ||
        policy.Equals(Seller, StringComparison.OrdinalIgnoreCase) ||
        policy.Equals(Staff, StringComparison.OrdinalIgnoreCase) ||
        policy.Equals(Admin, StringComparison.OrdinalIgnoreCase) ||
        policy.Equals(StaffOrAdmin, StringComparison.OrdinalIgnoreCase) ||
        policy.Equals(SellerOrStaffOrAdmin, StringComparison.OrdinalIgnoreCase);
}

// Control-plane endpoints must always authenticate with a real administrator.
// Bypass mode is only for business APIs backed by Mock services.
public sealed class DisableMockAuthorizationBypass;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class DisableMockAuthorizationBypassAttribute : Attribute;

public static class GatewayAuthorizationExtensions
{
    public static bool AllowsMockAuthorizationBypass(this HttpContext context) =>
        context.GetEndpoint()?.Metadata.GetMetadata<DisableMockAuthorizationBypass>() is null &&
        context.GetEndpoint()?.Metadata.GetMetadata<DisableMockAuthorizationBypassAttribute>() is null;
}
