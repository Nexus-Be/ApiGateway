namespace Nexus.Gateway.HealthProber;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

public sealed class ServiceHealthState
{
    public string ServiceName { get; }
    public bool IsEnabled { get; set; }
    public ProberRunStatus Status { get; set; } = ProberRunStatus.Disabled;
    public int ConsecutiveFailures { get; set; }
    public int AutoRecoveryCount { get; set; }
    public DateTime? LastCheckTime { get; set; }
    public DateTime? StartedAt { get; set; }
    public string? LastError { get; set; }

    private readonly ConcurrentQueue<HealthCheckSample> _samples = new();
    private const int MaxSamples = 30;

    public ServiceHealthState(string serviceName)
    {
        ServiceName = serviceName;
    }

    public void AddSample(HealthCheckSample sample)
    {
        _samples.Enqueue(sample);
        while (_samples.Count > MaxSamples && _samples.TryDequeue(out _))
        {
        }
    }

    public IReadOnlyList<HealthCheckSample> GetRecentSamples() => _samples.ToArray();

    public void Reset()
    {
        ConsecutiveFailures = 0;
        LastError = null;
    }
}
