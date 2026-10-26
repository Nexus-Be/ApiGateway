namespace Nexus.Gateway.Dtos.HealthProber;

using System;
using System.Collections.Generic;

public sealed record DatabaseProberToggleDto(
    bool Enabled,
    string? ConnectionString = null);

public sealed record DatabaseHealthStatusDto(
    string Service,
    bool IsEnabled,
    string Status,
    string TargetAddress,
    DateTimeOffset? LastCheckedAt,
    long LastLatencyMs,
    int ConsecutiveFailures,
    int TotalChecks,
    int TotalFailures,
    string? LastError);

public sealed record CheckHealServiceHealthDto(
    string Service,
    string Status,
    bool IsHealthy,
    long LatencyMs,
    string TargetUrl,
    DateTimeOffset CheckedAt,
    string? Details);

public sealed record CheckHealTargetDto(
    string Name,
    string Kind,
    bool IsEnabled,
    string TargetAddress,
    string CurrentStatus,
    DateTimeOffset? LastCheckedAt,
    long LastLatencyMs,
    int ConsecutiveFailures,
    int TotalChecks,
    int TotalFailures,
    string? LastError);

public sealed record CheckHealDashboardSummaryDto(
    string OverallStatus,
    int TotalServices,
    int EnabledServices,
    int HealthyServices,
    int TotalDatabases,
    int EnabledDatabases,
    int HealthyDatabases,
    int CronIntervalSeconds,
    DateTimeOffset Timestamp);

public sealed record CheckHealDashboardDto(
    CheckHealDashboardSummaryDto Summary,
    IReadOnlyList<CheckHealTargetDto> Services,
    IReadOnlyList<CheckHealTargetDto> Databases);

public sealed record CheckHealManualCheckDto(
    string Name,
    string Kind,
    string Status,
    long LatencyMs,
    string? Message,
    string? Error,
    DateTimeOffset Timestamp);
