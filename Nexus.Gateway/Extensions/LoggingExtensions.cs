namespace Nexus.Gateway.Extensions;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nexus.Gateway.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.PeriodicBatching;

public static class LoggingExtensions
{
    public static WebApplicationBuilder AddGatewayLogging(this WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;
        var batchOptions = new LogBatchOptions();
        configuration.GetSection("Serilog:Batching").Bind(batchOptions);

        // Bind root Serilog settings if available
        if (configuration.GetSection("Serilog:FilePath").Value is string filePath && !string.IsNullOrWhiteSpace(filePath))
        {
            batchOptions.FilePath = filePath;
        }

        builder.Services.AddHttpClient("LogBatchSender");

        builder.Host.UseSerilog((context, services, loggerConfiguration) =>
        {
            loggerConfiguration
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "Nexus.Gateway")
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");

            // 1. Lưu dưới dạng file rolling
            if (batchOptions.WriteToFile)
            {
                var resolvedFilePath = string.IsNullOrWhiteSpace(batchOptions.FilePath)
                    ? "logs/gateway-.log"
                    : batchOptions.FilePath;

                loggerConfiguration.WriteTo.File(
                    path: resolvedFilePath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14,
                    fileSizeLimitBytes: 10 * 1024 * 1024,
                    rollOnFileSizeLimit: true,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
            }

            // 2. Gom cụm và gửi theo cụm (Periodic Batching Sink)
            if (batchOptions.Enabled)
            {
                var httpFactory = services.GetService<IHttpClientFactory>();
                var batchSink = new GatewayBatchLogSink(batchOptions, httpFactory);
                var periodicBatchOptions = new PeriodicBatchingSinkOptions
                {
                    BatchSizeLimit = batchOptions.BatchSizeLimit > 0 ? batchOptions.BatchSizeLimit : 50,
                    Period = TimeSpan.FromSeconds(batchOptions.PeriodSeconds > 0 ? batchOptions.PeriodSeconds : 3),
                    QueueLimit = batchOptions.QueueLimit > 0 ? batchOptions.QueueLimit : 10000,
                    EagerlyEmitFirstEvent = false
                };

                loggerConfiguration.WriteTo.Sink(new PeriodicBatchingSink(batchSink, periodicBatchOptions));
            }
        });

        return builder;
    }
}
