namespace Nexus.Gateway.MockEngine;

public static class MockRouteMatcher
{
    public static MockRouteMatch Match(MockEndpointDefinition definition, string httpMethod, string requestPath)
    {
        if (!string.Equals(definition.NormalizedMethod, httpMethod, StringComparison.OrdinalIgnoreCase))
        {
            return MockRouteMatch.Failed;
        }

        var defSegments = TrimAndSplit(definition.NormalizedPath);
        var reqSegments = TrimAndSplit(requestPath);

        if (defSegments.Length != reqSegments.Length)
        {
            return MockRouteMatch.Failed;
        }

        var routeValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < defSegments.Length; i++)
        {
            var defSeg = defSegments[i];
            var reqSeg = reqSegments[i];

            if (defSeg.StartsWith('{') && defSeg.EndsWith('}'))
            {
                var paramName = defSeg[1..^1].Trim();
                // If parameter has type constraint like {id:guid}, strip constraint
                var colonIndex = paramName.IndexOf(':');
                if (colonIndex > 0)
                {
                    paramName = paramName[..colonIndex];
                }
                routeValues[paramName] = reqSeg;
            }
            else if (!string.Equals(defSeg, reqSeg, StringComparison.OrdinalIgnoreCase))
            {
                return MockRouteMatch.Failed;
            }
        }

        return MockRouteMatch.Success(definition, routeValues);
    }

    private static string[] TrimAndSplit(string path)
    {
        var cleanPath = path.Split('?')[0].Trim('/');
        if (string.IsNullOrEmpty(cleanPath))
        {
            return [];
        }
        return cleanPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
    }
}
