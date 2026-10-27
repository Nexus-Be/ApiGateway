namespace Nexus.Gateway.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

public static class TelemetryExtension
{
    public static IServiceCollection AddGatewayTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? configuration["Otel:Endpoint"];

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("nexus-gateway"))
            .WithTracing(t =>
            {
                t.AddAspNetCoreInstrumentation()
                 .AddHttpClientInstrumentation()
                 .AddSource(BackendDispatcher.ActivityName);

                if (!string.IsNullOrEmpty(otlpEndpoint))
                {
                    t.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
                }
            })
            .WithMetrics(m =>
            {
                m.AddAspNetCoreInstrumentation()
                 .AddHttpClientInstrumentation()
                 .AddMeter("Microsoft.AspNetCore.RateLimiting");

                if (!string.IsNullOrEmpty(otlpEndpoint))
                {
                    m.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
                }
            });

        return services;
    }
}
