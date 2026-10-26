namespace Nexus.Gateway.Clients;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nexus.Gateway.Dtos.HealthProber;

public interface ICheckHealServiceClient
{
    Task<CheckHealDashboardDto?> GetDashboardAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CheckHealTargetDto>> GetServicesAsync(CancellationToken ct = default);
    Task<CheckHealTargetDto?> GetServiceAsync(string serviceName, CancellationToken ct = default);
    Task<CheckHealTargetDto?> ToggleServiceAsync(string serviceName, bool enabled, string? url = null, CancellationToken ct = default);
    Task<CheckHealManualCheckDto?> CheckServiceAsync(string serviceName, CancellationToken ct = default);

    Task<IReadOnlyList<CheckHealTargetDto>> GetDatabasesAsync(CancellationToken ct = default);
    Task<CheckHealTargetDto?> GetDatabaseAsync(string databaseName, CancellationToken ct = default);
    Task<CheckHealTargetDto?> ToggleDatabaseAsync(string databaseName, bool enabled, string? connectionString = null, CancellationToken ct = default);
    Task<CheckHealManualCheckDto?> CheckDatabaseAsync(string databaseName, CancellationToken ct = default);

    Task<bool> BulkToggleAsync(bool enabled, string? kind = null, CancellationToken ct = default);

    Task<CheckHealServiceHealthDto> CheckHealthCheckServiceAsync(CancellationToken ct = default);
}
