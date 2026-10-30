namespace Nexus.Gateway.HealthProber;

using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Gateway.Clients;

public static class HealthProberExtensions
{
    public static IServiceCollection AddGatewayHealthProber(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<HealthProberOptions>(options =>
        {
            configuration.GetSection(HealthProberOptions.SectionName).Bind(options);
            var envKey = configuration["CHECKHEAL_API_KEY"];
            if (!string.IsNullOrWhiteSpace(envKey))
            {
                options.ApiKey = envKey;
            }
        });

        services.AddHttpClient("HealthProberClient");
        services.AddHttpClient("HealthCheckService", (sp, client) =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            var proberOpts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthProberOptions>>().Value;
            var config = sp.GetRequiredService<IConfiguration>();
            var apiKey = !string.IsNullOrWhiteSpace(proberOpts.ApiKey)
                ? proberOpts.ApiKey
                : config["CHECKHEAL_API_KEY"] ?? config["CheckHeal:ApiKey"] ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("X-Api-Key", apiKey);
            }
        });

        services.AddSingleton<ICheckHealServiceClient, CheckHealServiceClient>();

        services.AddSingleton<IHealthIncidentLogger, FileRollingHealthIncidentLogger>();
        services.AddSingleton<IDockerContainerRecoveryService, DockerContainerRecoveryService>();

        services.AddSingleton<HealthProberManager>();
        services.AddSingleton<IHealthProberManager>(sp => sp.GetRequiredService<HealthProberManager>());
        services.AddHostedService(sp => sp.GetRequiredService<HealthProberManager>());

        return services;
    }
}
