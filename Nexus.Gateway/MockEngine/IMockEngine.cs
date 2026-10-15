namespace Nexus.Gateway.MockEngine;

public interface IMockEngine
{
    IReadOnlyList<MockEndpointDefinition> GetDefinitions();

    MockRouteMatch Match(string httpMethod, string requestPath);

    void Reload();
}
