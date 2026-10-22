namespace Nexus.Gateway.HealthProber;

using System;
using System.Threading.Tasks;

public interface IHealthIncidentLogger
{
    Task LogIncidentAsync(
        string serviceName,
        ProberRunStatus status,
        int consecutiveFailures,
        int threshold,
        string message,
        Exception? exception = null);
}
