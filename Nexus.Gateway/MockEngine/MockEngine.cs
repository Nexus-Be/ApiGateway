using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Nexus.Gateway.MockEngine;

public sealed class MockEngine : IMockEngine, IDisposable
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<MockEngine> _logger;
    private readonly string _mocksDirectory;
    private readonly List<MockEndpointDefinition> _definitions = [];
    private readonly object _lock = new();
    private FileSystemWatcher? _watcher;
    private Timer? _debounceTimer;

    public MockEngine(IWebHostEnvironment environment, ILogger<MockEngine> logger)
    {
        _environment = environment;
        _logger = logger;
        _mocksDirectory = Path.Combine(_environment.ContentRootPath, "Mocks");

        Reload();
        InitializeWatcher();
    }

    public IReadOnlyList<MockEndpointDefinition> GetDefinitions()
    {
        lock (_lock)
        {
            return _definitions.ToList();
        }
    }

    public MockRouteMatch Match(string httpMethod, string requestPath)
    {
        lock (_lock)
        {
            foreach (var def in _definitions)
            {
                var match = MockRouteMatcher.Match(def, httpMethod, requestPath);
                if (match.IsMatch)
                {
                    return match;
                }
            }
        }

        return MockRouteMatch.Failed;
    }

    public void Reload()
    {
        lock (_lock)
        {
            _definitions.Clear();

            if (!Directory.Exists(_mocksDirectory))
            {
                _logger.LogWarning("Mocks directory not found at: {MocksDir}", _mocksDirectory);
                return;
            }

            var files = Directory.GetFiles(_mocksDirectory, "*.json", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".mock.json", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".def.json", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var file in files)
            {
                try
                {
                    LoadDefinitionFile(file);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load mock definition file: {File}", file);
                }
            }

            // Prioritize literal routes over parameterized routes (e.g. /users/me before /users/{id})
            _definitions.Sort(CompareRoutePrecedence);

            _logger.LogInformation("MockEngine loaded {Count} mock endpoint definitions from {Dir}", _definitions.Count, _mocksDirectory);
        }
    }

    private void LoadDefinitionFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        using var doc = JsonDocument.Parse(json);
        var dir = Path.GetDirectoryName(filePath) ?? _mocksDirectory;

        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var def = JsonSerializer.Deserialize<MockEndpointDefinition>(item.GetRawText());
                if (def != null)
                {
                    ProcessAndAddDefinition(def, filePath, dir);
                }
            }
        }
        else if (doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            var def = JsonSerializer.Deserialize<MockEndpointDefinition>(json);
            if (def != null)
            {
                ProcessAndAddDefinition(def, filePath, dir);
            }
        }
    }

    private void ProcessAndAddDefinition(MockEndpointDefinition def, string filePath, string dir)
    {
        def.SourceFilePath = filePath;

        // Auto-infer Service prefix if omitted: e.g. /api/v1/catalog/products => "catalog"
        if (string.IsNullOrWhiteSpace(def.Service))
        {
            def.Service = InferServiceFromPath(def.Path);
        }

        // Resolve external responseFile if provided
        if (!string.IsNullOrWhiteSpace(def.ResponseFile) && def.Response is null)
        {
            var respPath = Path.IsPathRooted(def.ResponseFile)
                ? def.ResponseFile
                : Path.Combine(dir, def.ResponseFile);

            if (File.Exists(respPath))
            {
                using var respDoc = JsonDocument.Parse(File.ReadAllText(respPath));
                def.Response = respDoc.RootElement.Clone();
            }
            else
            {
                _logger.LogWarning("Referenced response file '{RespPath}' in '{FilePath}' does not exist.", respPath, filePath);
            }
        }

        // Resolve external requestFile if provided
        if (!string.IsNullOrWhiteSpace(def.RequestFile) && def.Request is null)
        {
            var reqPath = Path.IsPathRooted(def.RequestFile)
                ? def.RequestFile
                : Path.Combine(dir, def.RequestFile);

            if (File.Exists(reqPath))
            {
                using var reqDoc = JsonDocument.Parse(File.ReadAllText(reqPath));
                def.Request = reqDoc.RootElement.Clone();
            }
        }

        _definitions.Add(def);
    }

    public static string InferServiceFromPath(string path)
    {
        var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var offset = 0;
        if (segments.Length >= 1 && segments[0].Equals("gateway", StringComparison.OrdinalIgnoreCase))
        {
            offset = 1;
        }

        // Standard MCS path: [gateway] / api / v1 / {service} / ...
        if (segments.Length >= offset + 3 && segments[offset].Equals("api", StringComparison.OrdinalIgnoreCase))
        {
            return GatewayOptions.ResolveCanonicalName(segments[offset + 2]);
        }
        if (segments.Length >= offset + 1)
        {
            return GatewayOptions.ResolveCanonicalName(segments[offset]);
        }
        return "default";
    }

    private void InitializeWatcher()
    {
        if (!Directory.Exists(_mocksDirectory)) return;

        try
        {
            _watcher = new FileSystemWatcher(_mocksDirectory, "*.json")
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
            };

            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileChanged;
            _watcher.Deleted += OnFileChanged;
            _watcher.Renamed += OnFileChanged;
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start FileSystemWatcher for Mocks directory.");
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(_ =>
        {
            _logger.LogInformation("Mocks directory file changed ({Name}), reloading MockEngine...", e.Name);
            Reload();
        }, null, 300, Timeout.Infinite);
    }

    private static int CompareRoutePrecedence(MockEndpointDefinition a, MockEndpointDefinition b)
    {
        // 1. Routes with fewer parameter segments {param} have higher precedence
        var aParams = CountRouteParameters(a.NormalizedPath);
        var bParams = CountRouteParameters(b.NormalizedPath);
        if (aParams != bParams)
        {
            return aParams.CompareTo(bParams);
        }

        // 2. Longer paths (more segments) have higher precedence
        var aSegs = CountSegments(a.NormalizedPath);
        var bSegs = CountSegments(b.NormalizedPath);
        if (aSegs != bSegs)
        {
            return bSegs.CompareTo(aSegs);
        }

        // 3. Stable alphabetical fallback
        return string.Compare(a.NormalizedPath, b.NormalizedPath, StringComparison.OrdinalIgnoreCase);
    }

    private static int CountRouteParameters(string path)
    {
        var count = 0;
        foreach (var seg in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (seg.StartsWith('{') && seg.EndsWith('}')) count++;
        }
        return count;
    }

    private static int CountSegments(string path) =>
        path.Split('/', StringSplitOptions.RemoveEmptyEntries).Length;

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounceTimer?.Dispose();
    }
}
