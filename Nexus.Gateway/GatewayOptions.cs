namespace Nexus.Gateway;

public sealed class GatewayOptions
{
    public const string SectionName = "Gateway";

    public static readonly string[] CanonicalServices =
    [
        "catalog",
        "commerce",
        "fulfillment",
        "users",
        "auction",
        "notification",
        "audit",
        "wallet"
    ];

    public Dictionary<string, BackendOptions> Services { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public int PermitLimit { get; set; } = 100;
    public int WindowSeconds { get; set; } = 60;
    public int ConcurrencyLimit { get; set; } = 100;
    public string RedisConnectionString { get; set; } = "localhost:6379";
    public string RedisKeyPrefix { get; set; } = "nexus:gateway";
    public bool RedisFailOpenInDevelopment { get; set; } = true;
    public string[] CorsOrigins { get; set; } = [];
    public string[] CorsMethods { get; set; } = ["GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"];
    public string[] CorsHeaders { get; set; } = ["*"];
    public bool CorsAllowCredentials { get; set; } = true;
    public bool AllowClientModeOverrideInDevelopment { get; set; } = true;
    public bool AllowAnonymousMockAuthInDevelopment { get; set; } = true;
    public bool AllowMockTokenAuth { get; set; } = true;
    public string DefaultMockUserId { get; set; } = "11111111-1111-1111-1111-000000000004";
    public string DefaultMockRole { get; set; } = "buyer";
    // Enforce keeps normal JWT/RBAC checks. Bypass is Development-only and
    // exists only to unblock frontend work against Mock services.
    public string? AuthMode { get; set; }

    // Read-only compatibility with older gateway.runtime.json files. New
    // configuration must use AuthMode (Enforce | Bypass).
    public string? GatewayMode { get; set; }

    public bool HasService(string name)
    {
        if (Services.ContainsKey(name)) return true;
        var canonical = ResolveCanonicalName(name);
        return Services.ContainsKey(canonical);
    }

    public BackendOptions? GetService(string name)
    {
        if (Services.TryGetValue(name, out var svc)) return svc;
        var canonical = ResolveCanonicalName(name);
        return Services.TryGetValue(canonical, out var cSvc) ? cSvc : null;
    }

    public static string ResolveCanonicalName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "default";
        var lower = name.Trim().ToLowerInvariant();
        return lower switch
        {
            "user" or "users" or "auth" => "users",
            "auction" or "auctions" => "auction",
            "notification" or "notifications" => "notification",
            "fulfillment" or "fulfillments" => "fulfillment",
            "category" or "categories" or "catalog" => "catalog",
            "order" or "orders" or "cart" or "commerce" => "commerce",
            "audit" or "audits" => "audit",
            "wallet" or "wallets" => "wallet",
            _ => lower
        };
    }

    public bool IsValid(bool development) =>
        PermitLimit > 0 && WindowSeconds > 0 && ConcurrencyLimit > 0 &&
        !string.IsNullOrWhiteSpace(RedisConnectionString) && !string.IsNullOrWhiteSpace(RedisKeyPrefix) &&
        CorsOrigins.Length > 0 && CorsOrigins.All(x => Uri.TryCreate(x, UriKind.Absolute, out var u) &&
            (u.Scheme is "https" or "http") && u.AbsolutePath == "/") &&
        CanonicalServices.All(HasService) &&
        IsValidAuthMode(AuthMode ?? GatewayMode ?? "Enforce", development) &&
        Services.Values.All(s => s.Mode is "Real" or "Mock" &&
            (development || s.Mode == "Real") &&
            s.TimeoutSeconds is >= 1 and <= 60 && s.MockDelayMs is >= 0 and <= 60000 &&
            (s.MockStatus == 200 || s.MockStatus is >= 400 and <= 599) &&
            Uri.TryCreate(s.Address, UriKind.Absolute, out var u) &&
            (u.Scheme == "https" || development && u.Scheme == "http"));

    private static bool IsValidAuthMode(string mode, bool development) =>
        string.Equals(mode, "Enforce", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(mode, "Standard", StringComparison.OrdinalIgnoreCase) ||
        (development && (string.Equals(mode, "Bypass", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(mode, "Mock", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(mode, "MockBypass", StringComparison.OrdinalIgnoreCase)));
}

public sealed class BackendOptions
{
    public string Mode { get; set; } = "Real";
    public string Address { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 5;
    public int MockDelayMs { get; set; } = 0;
    public int MockStatus { get; set; } = 200;
}
