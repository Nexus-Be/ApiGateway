namespace Nexus.Gateway.Tests;

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexus.Gateway.HealthProber;
using Xunit;

public sealed class HealthProberUnitTests
{
    [Fact]
    public void ServiceHealthState_keeps_ring_buffer_capped()
    {
        var state = new ServiceHealthState("catalog");
        for (int i = 0; i < 50; i++)
        {
            state.AddSample(new HealthCheckSample(
                Timestamp: DateTime.UtcNow,
                IsSuccess: true,
                StatusCode: 200,
                LatencyMs: 15));
        }

        var samples = state.GetRecentSamples();
        Assert.Equal(30, samples.Count);
    }

    [Fact]
    public async Task FileRollingHealthIncidentLogger_rotates_when_line_limit_reached()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "nexus_health_tests_" + Guid.NewGuid().ToString("N"));
        try
        {
            var options = Options.Create(new HealthProberOptions
            {
                LogDirectory = tempDir,
                MaxLinesPerLogFile = 5 // Giới hạn 5 dòng để kiểm tra xoay file
            });

            var logger = new FileRollingHealthIncidentLogger(
                new TestOptionsMonitor<HealthProberOptions>(options.Value),
                NullLogger<FileRollingHealthIncidentLogger>.Instance);

            // Ghi 12 dòng log sự cố
            for (int i = 1; i <= 12; i++)
            {
                await logger.LogIncidentAsync(
                    serviceName: "catalog",
                    status: ProberRunStatus.Degraded,
                    consecutiveFailures: i,
                    threshold: 5,
                    message: $"Probe failure #{i}");
            }

            var todayPrefix = DateTime.UtcNow.ToString("yyyyMMdd");
            var files = Directory.GetFiles(tempDir, $"incident-{todayPrefix}-*.log");
            Array.Sort(files);

            // Với 12 dòng và maxLines = 5: file 1 có 5 dòng, file 2 có 5 dòng, file 3 có 2 dòng -> tạo ra đúng 3 file
            Assert.Equal(3, files.Length);
            Assert.Equal(5, File.ReadAllLines(files[0]).Length);
            Assert.Equal(5, File.ReadAllLines(files[1]).Length);
            Assert.Equal(2, File.ReadAllLines(files[2]).Length);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public TestOptionsMonitor(T currentValue) => CurrentValue = currentValue;
        public T CurrentValue { get; }
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
