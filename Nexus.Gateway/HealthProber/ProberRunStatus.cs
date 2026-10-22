namespace Nexus.Gateway.HealthProber;

public enum ProberRunStatus
{
    Disabled,
    Starting,
    Healthy,
    Degraded,
    Unhealthy,
    Recovering
}
