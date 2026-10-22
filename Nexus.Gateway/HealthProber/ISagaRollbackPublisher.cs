namespace Nexus.Gateway.HealthProber;

using System.Threading;
using System.Threading.Tasks;

public interface ISagaRollbackPublisher
{
    Task PublishServiceUnhealthyEventAsync(string serviceName, string reason, CancellationToken cancellationToken = default);
}
