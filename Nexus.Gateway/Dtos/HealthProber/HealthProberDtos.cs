namespace Nexus.Gateway.Dtos.HealthProber;

using System;
using System.Collections.Generic;
using Nexus.Gateway.HealthProber;

public sealed record ServiceProberToggleDto(
    bool Enabled,
    int? IntervalSeconds = null,
    int? FailureThreshold = null);

public sealed record BulkProberToggleDto(
    bool EnableAll);

public sealed record ServiceHealthStatusDto(
    string Service,
    bool IsEnabled,
    string Status,
    int ConsecutiveFailures,
    int AutoRecoveryCount,
    DateTime? LastCheckTime,
    string? LastError,
    IReadOnlyList<HealthCheckSample> RecentSamples);
