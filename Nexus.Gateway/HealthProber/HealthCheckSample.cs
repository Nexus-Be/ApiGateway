namespace Nexus.Gateway.HealthProber;

public sealed record HealthCheckSample(
    DateTime Timestamp,
    bool IsSuccess,
    int StatusCode,
    long LatencyMs,
    string? ErrorMessage = null);
