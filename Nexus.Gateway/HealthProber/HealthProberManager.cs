namespace Nexus.Gateway.HealthProber;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class HealthProberManager : IHealthProberManager, IHostedService
{
    private readonly IOptionsMonitor<GatewayOptions> _gatewayOptions;
    private readonly IOptionsMonitor<HealthProberOptions> _proberOptions;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHealthIncidentLogger _incidentLogger;
    private readonly IDockerContainerRecoveryService _recoveryService;
    private readonly ISagaRollbackPublisher _sagaPublisher;
    private readonly ILogger<HealthProberManager> _logger;

    private readonly ConcurrentDictionary<string, ServiceHealthState> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _workers = new(StringComparer.OrdinalIgnoreCase);

    public HealthProberManager(
        IOptionsMonitor<GatewayOptions> gatewayOptions,
        IOptionsMonitor<HealthProberOptions> proberOptions,
        IHttpClientFactory httpClientFactory,
        IHealthIncidentLogger incidentLogger,
        IDockerContainerRecoveryService recoveryService,
        ISagaRollbackPublisher sagaPublisher,
        ILogger<HealthProberManager> logger)
    {
        _gatewayOptions = gatewayOptions;
        _proberOptions = proberOptions;
        _httpClientFactory = httpClientFactory;
        _incidentLogger = incidentLogger;
        _recoveryService = recoveryService;
        _sagaPublisher = sagaPublisher;
        _logger = logger;

        foreach (var svc in GatewayOptions.CanonicalServices)
        {
            _states[svc] = new ServiceHealthState(svc);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("HealthProberManager initialized. Probers for {Count} services are registered and default to Disabled.", _states.Count);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var kvp in _workers)
        {
            try
            {
                kvp.Value.Cancel();
                kvp.Value.Dispose();
            }
            catch
            {
            }
        }
        _workers.Clear();
        return Task.CompletedTask;
    }

    public IReadOnlyList<ServiceHealthState> GetAllStates() => _states.Values.ToList();

    public ServiceHealthState? GetState(string serviceName)
    {
        var canonical = GatewayOptions.ResolveCanonicalName(serviceName);
        return _states.TryGetValue(canonical, out var state) ? state : null;
    }

    public ProberRunStatus GetStatus(string serviceName)
    {
        var state = GetState(serviceName);
        return state?.Status ?? ProberRunStatus.Disabled;
    }

    public bool IsServiceAvailable(string serviceName)
    {
        var state = GetState(serviceName);
        if (state == null) return true;

        // Nếu service bị Unhealthy hoặc đang trong quá trình Recovering container thì chặn ngay
        return state.Status != ProberRunStatus.Unhealthy && state.Status != ProberRunStatus.Recovering;
    }

    public Task<bool> EnableServiceProberAsync(string serviceName, int? intervalSeconds = null, int? threshold = null)
    {
        var canonical = GatewayOptions.ResolveCanonicalName(serviceName);
        if (!_states.TryGetValue(canonical, out var state))
        {
            return Task.FromResult(false);
        }

        // Dừng worker cũ nếu đang chạy
        if (_workers.TryRemove(canonical, out var oldCts))
        {
            try
            {
                oldCts.Cancel();
                oldCts.Dispose();
            }
            catch
            {
            }
        }

        var opts = _proberOptions.CurrentValue;
        var interval = intervalSeconds ?? opts.IntervalSeconds;
        var failThreshold = threshold ?? opts.FailureThreshold;

        state.IsEnabled = true;
        state.Status = ProberRunStatus.Starting;
        state.StartedAt = DateTime.UtcNow;
        state.ConsecutiveFailures = 0;
        state.LastError = null;

        var newCts = new CancellationTokenSource();
        _workers[canonical] = newCts;

        _ = Task.Run(() => RunProberLoopAsync(canonical, state, interval, failThreshold, newCts.Token));

        _logger.LogInformation("Health Prober enabled for '{Service}'. Interval: {Interval}s, Threshold: {Threshold}", canonical, interval, failThreshold);
        return Task.FromResult(true);
    }

    public Task<bool> DisableServiceProberAsync(string serviceName)
    {
        var canonical = GatewayOptions.ResolveCanonicalName(serviceName);
        if (!_states.TryGetValue(canonical, out var state))
        {
            return Task.FromResult(false);
        }

        if (_workers.TryRemove(canonical, out var cts))
        {
            try
            {
                cts.Cancel();
                cts.Dispose();
            }
            catch
            {
            }
        }

        state.IsEnabled = false;
        state.Status = ProberRunStatus.Disabled;
        state.StartedAt = null;

        _logger.LogInformation("Health Prober disabled for '{Service}'", canonical);
        return Task.FromResult(true);
    }

    public async Task SetBulkProberAsync(bool enableAll)
    {
        foreach (var svc in GatewayOptions.CanonicalServices)
        {
            if (enableAll)
            {
                await EnableServiceProberAsync(svc);
            }
            else
            {
                await DisableServiceProberAsync(svc);
            }
        }
    }

    public async Task<HealthCheckSample> ProbeOnceAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        var canonical = GatewayOptions.ResolveCanonicalName(serviceName);
        var svcConfig = _gatewayOptions.CurrentValue.GetService(canonical);
        var opts = _proberOptions.CurrentValue;

        var baseAddress = svcConfig?.Address;
        if (string.IsNullOrWhiteSpace(baseAddress))
        {
            return new HealthCheckSample(
                Timestamp: DateTime.UtcNow,
                IsSuccess: false,
                StatusCode: 0,
                LatencyMs: 0,
                ErrorMessage: $"No address configured for service '{canonical}'");
        }

        var client = _httpClientFactory.CreateClient("HealthProberClient");
        var timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds > 0 ? opts.TimeoutSeconds : 30);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        var probeUrl = $"{baseAddress.TrimEnd('/')}/health";
        var sw = Stopwatch.StartNew();

        try
        {
            using var response = await client.GetAsync(probeUrl, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            sw.Stop();

            var isSuccess = response.IsSuccessStatusCode;
            return new HealthCheckSample(
                Timestamp: DateTime.UtcNow,
                IsSuccess: isSuccess,
                StatusCode: (int)response.StatusCode,
                LatencyMs: sw.ElapsedMilliseconds,
                ErrorMessage: isSuccess ? null : $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            return new HealthCheckSample(
                Timestamp: DateTime.UtcNow,
                IsSuccess: false,
                StatusCode: 504,
                LatencyMs: sw.ElapsedMilliseconds,
                ErrorMessage: $"Health check timed out after {opts.TimeoutSeconds}s");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new HealthCheckSample(
                Timestamp: DateTime.UtcNow,
                IsSuccess: false,
                StatusCode: 0,
                LatencyMs: sw.ElapsedMilliseconds,
                ErrorMessage: ex.Message);
        }
    }

    private async Task RunProberLoopAsync(
        string serviceName,
        ServiceHealthState state,
        int intervalSeconds,
        int failureThreshold,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var sample = await ProbeOnceAsync(serviceName, cancellationToken);
                state.LastCheckTime = sample.Timestamp;
                state.AddSample(sample);

                var opts = _proberOptions.CurrentValue;
                var startPeriod = TimeSpan.FromSeconds(opts.StartPeriodSeconds > 0 ? opts.StartPeriodSeconds : 60);
                var isUnderStartPeriod = state.StartedAt.HasValue && (DateTime.UtcNow - state.StartedAt.Value) < startPeriod;

                if (sample.IsSuccess)
                {
                    state.Status = ProberRunStatus.Healthy;
                    state.ConsecutiveFailures = 0;
                    state.AutoRecoveryCount = 0; // Đã sống lại bình thường, reset số lần auto recovery
                    state.LastError = null;
                }
                else
                {
                    state.LastError = sample.ErrorMessage;

                    if (isUnderStartPeriod)
                    {
                        // Đang trong thời gian ân hạn (StartPeriod) -> Chưa tính lỗi vào Retries
                        state.Status = ProberRunStatus.Starting;
                        _logger.LogInformation("Service '{Service}' probe failed during StartPeriod grace window: {Error}", serviceName, sample.ErrorMessage);
                    }
                    else
                    {
                        state.ConsecutiveFailures++;

                        if (state.ConsecutiveFailures < failureThreshold)
                        {
                            state.Status = ProberRunStatus.Degraded;
                            await _incidentLogger.LogIncidentAsync(
                                serviceName,
                                ProberRunStatus.Degraded,
                                state.ConsecutiveFailures,
                                failureThreshold,
                                sample.ErrorMessage ?? "Degraded probe response");
                        }
                        else
                        {
                            var wasUnhealthy = state.Status == ProberRunStatus.Unhealthy;
                            state.Status = ProberRunStatus.Unhealthy;

                            await _incidentLogger.LogIncidentAsync(
                                serviceName,
                                ProberRunStatus.Unhealthy,
                                state.ConsecutiveFailures,
                                failureThreshold,
                                sample.ErrorMessage ?? "Failure threshold exceeded, service declared UNHEALTHY");

                            if (!wasUnhealthy)
                            {
                                await TriggerIncidentResponseAsync(serviceName, state, sample.ErrorMessage ?? "Failure threshold reached", cancellationToken);
                            }
                        }
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds > 0 ? intervalSeconds : 60), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in prober loop for '{Service}'", serviceName);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task TriggerIncidentResponseAsync(
        string serviceName,
        ServiceHealthState state,
        string reason,
        CancellationToken cancellationToken)
    {
        _logger.LogCritical("Service '{Service}' is UNHEALTHY! Initiating Circuit Breaker, Saga Rollback and Container Recovery.", serviceName);

        // 1. Kích hoạt Saga Rollback Event lên RabbitMQ
        try
        {
            await _sagaPublisher.PublishServiceUnhealthyEventAsync(serviceName, reason, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish ServiceUnhealthyIntegrationEvent for '{Service}'", serviceName);
        }

        // 2. Kích hoạt Docker Auto-Recovery nếu được bật
        var opts = _proberOptions.CurrentValue;
        if (opts.AutoRecoverContainer)
        {
            if (state.AutoRecoveryCount < opts.MaxAutoRecoverAttempts)
            {
                state.AutoRecoveryCount++;
                state.Status = ProberRunStatus.Recovering;

                await _incidentLogger.LogIncidentAsync(
                    serviceName,
                    ProberRunStatus.Recovering,
                    state.ConsecutiveFailures,
                    opts.FailureThreshold,
                    $"Triggering Docker container auto-recovery (Attempt {state.AutoRecoveryCount}/{opts.MaxAutoRecoverAttempts})");

                var success = await _recoveryService.RecoverContainerAsync(serviceName, cancellationToken);
                if (success)
                {
                    // Chuyển sang Starting và reset StartPeriod để container có thời gian khởi động
                    state.Status = ProberRunStatus.Starting;
                    state.StartedAt = DateTime.UtcNow;
                    state.ConsecutiveFailures = 0;
                    _logger.LogInformation("Container for '{Service}' successfully restarted. Entering StartPeriod.", serviceName);
                }
                else
                {
                    _logger.LogWarning("Container auto-recovery for '{Service}' failed or was skipped.", serviceName);
                }
            }
            else
            {
                _logger.LogError("Service '{Service}' reached MaxAutoRecoverAttempts ({Max}). Halting auto-recovery to prevent crash loop.", serviceName, opts.MaxAutoRecoverAttempts);
            }
        }
    }
}
