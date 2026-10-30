namespace Nexus.Gateway.HealthProber;

public sealed class HealthProberOptions
{
    public const string SectionName = "Gateway:HealthProber";

    /// <summary>
    /// Probe interval in seconds (default: 60s).
    /// </summary>
    public int IntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Maximum response timeout in seconds (default: 30s).
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Number of consecutive failures before marking Unhealthy (default: 5).
    /// </summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// Container startup grace period in seconds (default: 60s).
    /// </summary>
    public int StartPeriodSeconds { get; set; } = 60;

    /// <summary>
    /// Automatically restart/recreate container when Unhealthy.
    /// </summary>
    public bool AutoRecoverContainer { get; set; } = true;

    /// <summary>
    /// Maximum consecutive auto-recovery attempts to prevent CrashLoopBackOff (default: 3).
    /// </summary>
    public int MaxAutoRecoverAttempts { get; set; } = 3;

    /// <summary>
    /// Directory path for incident log storage at Gateway.
    /// </summary>
    public string LogDirectory { get; set; } = "logs/health";

    /// <summary>
    /// Maximum log lines per file before rotation (default: 1000).
    /// </summary>
    public int MaxLinesPerLogFile { get; set; } = 1000;

    /// <summary>
    /// Secure API Key used to authenticate CheckHeal operations across Gateway and CheckHealService.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
}
