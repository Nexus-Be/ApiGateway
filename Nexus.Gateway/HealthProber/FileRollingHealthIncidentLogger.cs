namespace Nexus.Gateway.HealthProber;

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class FileRollingHealthIncidentLogger : IHealthIncidentLogger
{
    private readonly IOptionsMonitor<HealthProberOptions> _options;
    private readonly ILogger<FileRollingHealthIncidentLogger> _logger;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    private string? _currentFilePath;
    private int _currentLineCount;
    private int _currentIndex;
    private string _currentDatePrefix = string.Empty;

    public FileRollingHealthIncidentLogger(
        IOptionsMonitor<HealthProberOptions> options,
        ILogger<FileRollingHealthIncidentLogger> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task LogIncidentAsync(
        string serviceName,
        ProberRunStatus status,
        int consecutiveFailures,
        int threshold,
        string message,
        Exception? exception = null)
    {
        var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");
        var level = status switch
        {
            ProberRunStatus.Unhealthy => "CRITICAL",
            ProberRunStatus.Degraded => "WARN",
            ProberRunStatus.Recovering => "INFO",
            _ => "WARN"
        };

        var sb = new StringBuilder();
        sb.Append($"[{timestamp}] [{level}] Service: {serviceName} | Status: {status} | Failures: {consecutiveFailures}/{threshold} | Message: {message}");

        if (exception != null)
        {
            sb.Append($" | Exception: {exception.GetType().Name}: {exception.Message}");
            if (!string.IsNullOrWhiteSpace(exception.StackTrace))
            {
                // Replace newlines in stacktrace for single-line log format
                var sanitizedTrace = exception.StackTrace.Replace("\r\n", " -> ").Replace("\n", " -> ");
                sb.Append($" | Trace: {sanitizedTrace}");
            }
        }

        var logLine = sb.ToString();

        // 1. Write log to Serilog / Console
        if (status == ProberRunStatus.Unhealthy)
        {
            _logger.LogError("[HEALTH UNHEALTHY] {LogLine}", logLine);
        }
        else if (status == ProberRunStatus.Degraded)
        {
            _logger.LogWarning("[HEALTH DEGRADED] {LogLine}", logLine);
        }
        else
        {
            _logger.LogInformation("[HEALTH EVENT] {LogLine}", logLine);
        }

        // 2. Write to incident file with line limit rolling mechanism
        await WriteToFileWithLineLimitAsync(logLine);
    }

    private async Task WriteToFileWithLineLimitAsync(string logLine)
    {
        var opts = _options.CurrentValue;
        var dir = opts.LogDirectory;
        var maxLines = opts.MaxLinesPerLogFile > 0 ? opts.MaxLinesPerLogFile : 1000;

        await _fileLock.WaitAsync();
        try
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var todayPrefix = DateTime.UtcNow.ToString("yyyyMMdd");

            // Initialize if new day or active file not set
            if (_currentFilePath == null || _currentDatePrefix != todayPrefix)
            {
                InitActiveFile(dir, todayPrefix, maxLines);
            }

            // Rotate to new file if current file reached line threshold
            if (_currentLineCount >= maxLines)
            {
                _currentIndex++;
                _currentFilePath = Path.Combine(dir, $"incident-{todayPrefix}-{_currentIndex:D4}.log");
                _currentLineCount = 0;
            }

            await File.AppendAllLinesAsync(_currentFilePath!, new[] { logLine }, Encoding.UTF8);
            _currentLineCount++;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write health incident to file {FilePath}", _currentFilePath);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private void InitActiveFile(string dir, string todayPrefix, int maxLines)
    {
        _currentDatePrefix = todayPrefix;

        // Discover matching incident-yyyyMMdd-XXXX.log files
        var pattern = $"incident-{todayPrefix}-*.log";
        var existingFiles = Directory.GetFiles(dir, pattern)
            .OrderBy(f => f)
            .ToList();

        if (existingFiles.Count == 0)
        {
            _currentIndex = 1;
            _currentFilePath = Path.Combine(dir, $"incident-{todayPrefix}-{_currentIndex:D4}.log");
            _currentLineCount = 0;
            return;
        }

        var lastFile = existingFiles.Last();
        var match = Regex.Match(Path.GetFileName(lastFile), $@"incident-{todayPrefix}-(\d+)\.log");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var idx))
        {
            _currentIndex = idx;
        }
        else
        {
            _currentIndex = existingFiles.Count;
        }

        _currentFilePath = lastFile;
        try
        {
            _currentLineCount = File.ReadLines(lastFile).Count();
        }
        catch
        {
            _currentLineCount = 0;
        }

        // If last file reached line limit, rotate
        if (_currentLineCount >= maxLines)
        {
            _currentIndex++;
            _currentFilePath = Path.Combine(dir, $"incident-{todayPrefix}-{_currentIndex:D4}.log");
            _currentLineCount = 0;
        }
    }
}
