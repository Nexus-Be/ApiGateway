namespace Nexus.Gateway.HealthProber;

using System.Threading;
using System.Threading.Tasks;

public interface IDockerContainerRecoveryService
{
    Task<bool> RecoverContainerAsync(string serviceName, CancellationToken cancellationToken = default);
}
