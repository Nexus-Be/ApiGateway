namespace Nexus.Gateway.Clients;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexus.Gateway.Dtos.HealthProber;

public sealed class CheckHealServiceClient : ICheckHealServiceClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<GatewayOptions> _gatewayOptions;
    private readonly ILogger<CheckHealServiceClient> _logger;

    // In-memory mock store for Mock mode / fallback during testing
    private readonly ConcurrentDictionary<string, CheckHealTargetDto> _mockServices = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CheckHealTargetDto> _mockDatabases = new(StringComparer.OrdinalIgnoreCase);

    public CheckHealServiceClient(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<GatewayOptions> gatewayOptions,
        ILogger<CheckHealServiceClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _gatewayOptions = gatewayOptions;
        _logger = logger;

        InitializeMockData();
    }

    private void InitializeMockData()
    {
        foreach (var svc in GatewayOptions.CanonicalServices)
        {
            _mockServices[svc] = new CheckHealTargetDto(
                Name: svc,
                Kind: "service",
                IsEnabled: false,
                TargetAddress: $"http://{svc}:8001/health",
                CurrentStatus: "disabled",
                LastCheckedAt: null,
                LastLatencyMs: 0,
                ConsecutiveFailures: 0,
                TotalChecks: 0,
                TotalFailures: 0,
                LastError: null);

            _mockDatabases[svc] = new CheckHealTargetDto(
                Name: svc,
                Kind: "database",
                IsEnabled: false,
                TargetAddress: "",
                CurrentStatus: "disabled",
                LastCheckedAt: null,
                LastLatencyMs: 0,
                ConsecutiveFailures: 0,
                TotalChecks: 0,
                TotalFailures: 0,
                LastError: null);
        }
    }

    private bool IsMockMode()
    {
        var svc = _gatewayOptions.CurrentValue.GetService("healthcheck");
        if (svc != null && string.Equals(svc.Mode, "Real", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Default to Mock in Development/Testing unless explicitly set to Real
        return true;
    }

    private string GetBaseUrl()
    {
        var svc = _gatewayOptions.CurrentValue.GetService("healthcheck");
        return !string.IsNullOrWhiteSpace(svc?.Address)
            ? svc.Address.TrimEnd('/')
            : "http://localhost:8009";
    }

    public async Task<CheckHealDashboardDto?> GetDashboardAsync(CancellationToken ct = default)
    {
        if (IsMockMode())
        {
            var services = _mockServices.Values.ToList();
            var databases = _mockDatabases.Values.ToList();
            return new CheckHealDashboardDto(
                Summary: new CheckHealDashboardSummaryDto(
                    OverallStatus: "healthy",
                    TotalServices: services.Count,
                    EnabledServices: services.Count(s => s.IsEnabled),
                    HealthyServices: services.Count(s => s.CurrentStatus == "healthy"),
                    TotalDatabases: databases.Count,
                    EnabledDatabases: databases.Count(d => d.IsEnabled),
                    HealthyDatabases: databases.Count(d => d.CurrentStatus == "healthy"),
                    CronIntervalSeconds: 60,
                    Timestamp: DateTimeOffset.UtcNow),
                Services: services,
                Databases: databases);
        }

        try
        {
            var client = _httpClientFactory.CreateClient("HealthCheckService");
            var url = $"{GetBaseUrl()}/api/v1/healthcheck/status";
            return await client.GetFromJsonAsync<CheckHealDashboardDto>(url, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call CheckHealService at {BaseUrl}", GetBaseUrl());
            return null;
        }
    }

    public async Task<IReadOnlyList<CheckHealTargetDto>> GetServicesAsync(CancellationToken ct = default)
    {
        if (IsMockMode())
        {
            return _mockServices.Values.ToList();
        }

        try
        {
            var client = _httpClientFactory.CreateClient("HealthCheckService");
            var url = $"{GetBaseUrl()}/api/v1/healthcheck/services";
            var result = await client.GetFromJsonAsync<List<CheckHealTargetDto>>(url, ct);
            return result ?? new List<CheckHealTargetDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get services from CheckHealService");
            return _mockServices.Values.ToList();
        }
    }

    public async Task<CheckHealTargetDto?> GetServiceAsync(string serviceName, CancellationToken ct = default)
    {
        var canonical = GatewayOptions.ResolveCanonicalName(serviceName);
        if (IsMockMode())
        {
            return _mockServices.TryGetValue(canonical, out var dto) ? dto : null;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("HealthCheckService");
            var url = $"{GetBaseUrl()}/api/v1/healthcheck/services/{canonical}";
            return await client.GetFromJsonAsync<CheckHealTargetDto>(url, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get service {Service} from CheckHealService", canonical);
            return _mockServices.TryGetValue(canonical, out var dto) ? dto : null;
        }
    }

    public async Task<CheckHealTargetDto?> ToggleServiceAsync(string serviceName, bool enabled, string? url = null, CancellationToken ct = default)
    {
        var canonical = GatewayOptions.ResolveCanonicalName(serviceName);
        if (IsMockMode())
        {
            if (_mockServices.TryGetValue(canonical, out var oldDto))
            {
                var updated = oldDto with
                {
                    IsEnabled = enabled,
                    CurrentStatus = enabled ? "healthy" : "disabled",
                    TargetAddress = url ?? oldDto.TargetAddress
                };
                _mockServices[canonical] = updated;
                return updated;
            }
            return null;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("HealthCheckService");
            var reqUrl = $"{GetBaseUrl()}/api/v1/healthcheck/services/{canonical}/toggle";
            var response = await client.PutAsJsonAsync(reqUrl, new { enabled, url }, ct);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<CheckHealTargetDto>(cancellationToken: ct);
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle service {Service} in CheckHealService", canonical);
            return null;
        }
    }

    public async Task<CheckHealManualCheckDto?> CheckServiceAsync(string serviceName, CancellationToken ct = default)
    {
        var canonical = GatewayOptions.ResolveCanonicalName(serviceName);
        if (IsMockMode())
        {
            return new CheckHealManualCheckDto(
                Name: canonical,
                Kind: "service",
                Status: "healthy",
                LatencyMs: 12,
                Message: $"Service '{canonical}' is Healthy. Latency: 12ms.",
                Error: null,
                Timestamp: DateTimeOffset.UtcNow);
        }

        try
        {
            var client = _httpClientFactory.CreateClient("HealthCheckService");
            var url = $"{GetBaseUrl()}/api/v1/healthcheck/services/{canonical}/check";
            var response = await client.PostAsync(url, null, ct);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<CheckHealManualCheckDto>(cancellationToken: ct);
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check service {Service} in CheckHealService", canonical);
            return null;
        }
    }

    public async Task<IReadOnlyList<CheckHealTargetDto>> GetDatabasesAsync(CancellationToken ct = default)
    {
        if (IsMockMode())
        {
            return _mockDatabases.Values.ToList();
        }

        try
        {
            var client = _httpClientFactory.CreateClient("HealthCheckService");
            var url = $"{GetBaseUrl()}/api/v1/healthcheck/databases";
            var result = await client.GetFromJsonAsync<List<CheckHealTargetDto>>(url, ct);
            return result ?? new List<CheckHealTargetDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get databases from CheckHealService");
            return _mockDatabases.Values.ToList();
        }
    }

    public async Task<CheckHealTargetDto?> GetDatabaseAsync(string databaseName, CancellationToken ct = default)
    {
        var canonical = GatewayOptions.ResolveCanonicalName(databaseName);
        if (IsMockMode())
        {
            return _mockDatabases.TryGetValue(canonical, out var dto) ? dto : null;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("HealthCheckService");
            var url = $"{GetBaseUrl()}/api/v1/healthcheck/databases/{canonical}";
            return await client.GetFromJsonAsync<CheckHealTargetDto>(url, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get database {Database} from CheckHealService", canonical);
            return _mockDatabases.TryGetValue(canonical, out var dto) ? dto : null;
        }
    }

    public async Task<CheckHealTargetDto?> ToggleDatabaseAsync(string databaseName, bool enabled, string? connectionString = null, CancellationToken ct = default)
    {
        var canonical = GatewayOptions.ResolveCanonicalName(databaseName);
        if (IsMockMode())
        {
            if (_mockDatabases.TryGetValue(canonical, out var oldDto))
            {
                var updated = oldDto with
                {
                    IsEnabled = enabled,
                    CurrentStatus = enabled ? "healthy" : "disabled",
                    TargetAddress = connectionString ?? oldDto.TargetAddress
                };
                _mockDatabases[canonical] = updated;
                return updated;
            }
            return null;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("HealthCheckService");
            var reqUrl = $"{GetBaseUrl()}/api/v1/healthcheck/databases/{canonical}/toggle";
            var response = await client.PutAsJsonAsync(reqUrl, new { enabled, connectionString }, ct);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<CheckHealTargetDto>(cancellationToken: ct);
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle database {Database} in CheckHealService", canonical);
            return null;
        }
    }

    public async Task<CheckHealManualCheckDto?> CheckDatabaseAsync(string databaseName, CancellationToken ct = default)
    {
        var canonical = GatewayOptions.ResolveCanonicalName(databaseName);
        if (IsMockMode())
        {
            return new CheckHealManualCheckDto(
                Name: canonical,
                Kind: "database",
                Status: "healthy",
                LatencyMs: 18,
                Message: $"Database for '{canonical}' connection alive. Keep-alive query SELECT 1 executed successfully.",
                Error: null,
                Timestamp: DateTimeOffset.UtcNow);
        }

        try
        {
            var client = _httpClientFactory.CreateClient("HealthCheckService");
            var url = $"{GetBaseUrl()}/api/v1/healthcheck/databases/{canonical}/check";
            var response = await client.PostAsync(url, null, ct);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<CheckHealManualCheckDto>(cancellationToken: ct);
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check database {Database} in CheckHealService", canonical);
            return null;
        }
    }

    public async Task<bool> BulkToggleAsync(bool enabled, string? kind = null, CancellationToken ct = default)
    {
        if (IsMockMode())
        {
            if (string.IsNullOrWhiteSpace(kind) || kind == "all" || kind == "service" || kind == "services")
            {
                foreach (var k in _mockServices.Keys.ToList())
                {
                    _mockServices[k] = _mockServices[k] with { IsEnabled = enabled, CurrentStatus = enabled ? "healthy" : "disabled" };
                }
            }

            if (string.IsNullOrWhiteSpace(kind) || kind == "all" || kind == "database" || kind == "databases")
            {
                foreach (var k in _mockDatabases.Keys.ToList())
                {
                    _mockDatabases[k] = _mockDatabases[k] with { IsEnabled = enabled, CurrentStatus = enabled ? "healthy" : "disabled" };
                }
            }
            return true;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("HealthCheckService");
            var url = $"{GetBaseUrl()}/api/v1/healthcheck/bulk-toggle";
            var response = await client.PutAsJsonAsync(url, new { enabled, kind }, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to bulk toggle in CheckHealService");
            return false;
        }
    }

    public async Task<CheckHealServiceHealthDto> CheckHealthCheckServiceAsync(CancellationToken ct = default)
    {
        var targetUrl = $"{GetBaseUrl()}/health/live";
        var sw = Stopwatch.StartNew();

        if (IsMockMode())
        {
            return new CheckHealServiceHealthDto(
                Service: "checkheal-service",
                Status: "healthy",
                IsHealthy: true,
                LatencyMs: 5,
                TargetUrl: targetUrl,
                CheckedAt: DateTimeOffset.UtcNow,
                Details: "CheckHealService is operating normally (Mock Mode).");
        }

        try
        {
            var client = _httpClientFactory.CreateClient("HealthCheckService");
            var response = await client.GetAsync(targetUrl, ct);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                return new CheckHealServiceHealthDto(
                    Service: "checkheal-service",
                    Status: "healthy",
                    IsHealthy: true,
                    LatencyMs: sw.ElapsedMilliseconds,
                    TargetUrl: targetUrl,
                    CheckedAt: DateTimeOffset.UtcNow,
                    Details: $"CheckHealService returned HTTP {(int)response.StatusCode} OK.");
            }

            return new CheckHealServiceHealthDto(
                Service: "checkheal-service",
                Status: "degraded",
                IsHealthy: false,
                LatencyMs: sw.ElapsedMilliseconds,
                TargetUrl: targetUrl,
                CheckedAt: DateTimeOffset.UtcNow,
                Details: $"CheckHealService returned non-success code: {(int)response.StatusCode}.");
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "Failed to reach CheckHealService at {TargetUrl}", targetUrl);
            return new CheckHealServiceHealthDto(
                Service: "checkheal-service",
                Status: "unhealthy",
                IsHealthy: false,
                LatencyMs: sw.ElapsedMilliseconds,
                TargetUrl: targetUrl,
                CheckedAt: DateTimeOffset.UtcNow,
                Details: $"Error reaching CheckHealService: {ex.Message}");
        }
    }
}
