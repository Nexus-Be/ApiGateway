namespace Nexus.Gateway.HealthProber;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface IHealthProberManager
{
    IReadOnlyList<ServiceHealthState> GetAllStates();
    ServiceHealthState? GetState(string serviceName);
    ProberRunStatus GetStatus(string serviceName);
    bool IsServiceAvailable(string serviceName);

    Task<bool> EnableServiceProberAsync(string serviceName, int? intervalSeconds = null, int? threshold = null);
    Task<bool> DisableServiceProberAsync(string serviceName);
    Task SetBulkProberAsync(bool enableAll);

    // Used for tests or manual triggers
    Task<HealthCheckSample> ProbeOnceAsync(string serviceName, CancellationToken cancellationToken = default);
}
