namespace Nexus.Gateway.HealthProber;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

public sealed class DockerContainerRecoveryService : IDockerContainerRecoveryService
{
    private readonly ILogger<DockerContainerRecoveryService> _logger;

    public DockerContainerRecoveryService(ILogger<DockerContainerRecoveryService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> RecoverContainerAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        var containerName = GetContainerName(serviceName);
        _logger.LogInformation("Attempting to recover container '{Container}' for service '{Service}' via Docker engine...", containerName, serviceName);

        try
        {
            // Check Docker socket existence
            var dockerSocketPath = "/var/run/docker.sock";
            if (!System.IO.File.Exists(dockerSocketPath))
            {
                _logger.LogWarning("Docker socket '{Socket}' not found. Container auto-recovery is only available when running with Docker socket mounted.", dockerSocketPath);
                return false;
            }

            // Execute docker restart command
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"restart {containerName}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null) return false;

            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode == 0)
            {
                _logger.LogInformation("Successfully restarted container '{Container}'", containerName);
                return true;
            }

            var err = await process.StandardError.ReadToEndAsync(cancellationToken);
            _logger.LogError("Docker command failed with code {Code}: {Error}", process.ExitCode, err);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recover container for service '{Service}'", serviceName);
            return false;
        }
    }

    public static string GetContainerName(string serviceName)
    {
        var lower = serviceName.ToLowerInvariant();
        return lower switch
        {
            "users" or "user" => "nexus-user-service",
            "catalog" => "nexus-catalog-service",
            "commerce" => "nexus-commerce-service",
            "log" => "nexus-log-service",
            "auction" => "nexus-auction-service",
            "fulfillment" => "nexus-fulfillment-service",
            "notification" => "nexus-notification-service",
            "wallet" => "nexus-wallet-service",
            _ => $"nexus-{lower}-service"
        };
    }
}
