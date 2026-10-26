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
        services.Configure<HealthProberOptions>(configuration.GetSection(HealthProberOptions.SectionName));

        services.AddHttpClient("HealthProberClient");
        services.AddHttpClient("HealthCheckService", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        services.AddSingleton<ICheckHealServiceClient, CheckHealServiceClient>();

        services.AddSingleton<IHealthIncidentLogger, FileRollingHealthIncidentLogger>();
        services.AddSingleton<IDockerContainerRecoveryService, DockerContainerRecoveryService>();
        services.AddSingleton<ISagaRollbackPublisher, SagaRollbackPublisher>();

        services.AddSingleton<HealthProberManager>();
        services.AddSingleton<IHealthProberManager>(sp => sp.GetRequiredService<HealthProberManager>());
        services.AddHostedService(sp => sp.GetRequiredService<HealthProberManager>());

        return services;
    }
}
