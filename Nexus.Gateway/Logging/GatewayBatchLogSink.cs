namespace Nexus.Gateway.Logging;

using System.Text.Json;
using Serilog.Events;
using Serilog.Sinks.PeriodicBatching;

public sealed class GatewayBatchLogSink : IBatchedLogEventSink
{
    private readonly LogBatchOptions _options;
    private readonly IHttpClientFactory? _httpClientFactory;

    public GatewayBatchLogSink(LogBatchOptions options, IHttpClientFactory? httpClientFactory = null)
    {
        _options = options;
        _httpClientFactory = httpClientFactory;
    }

    public async Task EmitBatchAsync(IEnumerable<LogEvent> batch)
    {
        var logEvents = batch.ToList();
        if (logEvents.Count == 0) return;

        try
        {
            var batchEntries = logEvents.Select(e => new
            {
                Timestamp = e.Timestamp.ToString("o"),
                Level = e.Level.ToString(),
                Message = e.RenderMessage(),
                Exception = e.Exception?.ToString(),
                Properties = e.Properties.ToDictionary(k => k.Key, v => v.Value.ToString())
            }).ToList();

            // 1. Save batch snapshot to file if configured
            if (_options.SaveBatchSnapshots)
            {
                var dir = _options.BatchDirectory;
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                var fileName = Path.Combine(dir, $"batch-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.json");
                var json = JsonSerializer.Serialize(batchEntries, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(fileName, json);
            }

            // 2. Send batch to remote service if configured
            if (!string.IsNullOrWhiteSpace(_options.RemoteEndpoint) && _httpClientFactory != null)
            {
                using var client = _httpClientFactory.CreateClient("LogBatchSender");
                client.Timeout = TimeSpan.FromSeconds(5);
                var jsonPayload = JsonSerializer.Serialize(batchEntries);
                var content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");
                _ = await client.PostAsync($"{_options.RemoteEndpoint.TrimEnd('/')}/api/v1/log/batch", content);
            }
        }
        catch
        {
            // Fail-safe: Never throw unhandled exception from logging sink
        }
    }

    public Task OnEmptyBatchAsync() => Task.CompletedTask;
}
