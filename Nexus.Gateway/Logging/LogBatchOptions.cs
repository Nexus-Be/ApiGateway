namespace Nexus.Gateway.Logging;

public sealed class LogBatchOptions
{
    public bool Enabled { get; set; } = true;
    public int BatchSizeLimit { get; set; } = 50;
    public int PeriodSeconds { get; set; } = 3;
    public int QueueLimit { get; set; } = 10000;
    public bool WriteToFile { get; set; } = true;
    public string FilePath { get; set; } = "logs/gateway-.log";
    public bool SaveBatchSnapshots { get; set; } = false;
    public string BatchDirectory { get; set; } = "logs/batches";
    public string? RemoteEndpoint { get; set; }
}
