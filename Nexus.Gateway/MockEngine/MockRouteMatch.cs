namespace Nexus.Gateway.MockEngine;

public sealed class MockRouteMatch
{
    public bool IsMatch { get; init; }
    public MockEndpointDefinition? Definition { get; init; }
    public Dictionary<string, string> RouteValues { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public static MockRouteMatch Failed => new() { IsMatch = false };

    public static MockRouteMatch Success(MockEndpointDefinition def, Dictionary<string, string> routeValues) =>
        new()
        {
            IsMatch = true,
            Definition = def,
            RouteValues = routeValues
        };
}
