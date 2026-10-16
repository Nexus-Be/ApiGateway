namespace Nexus.Gateway.Extensions;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public static class ConfigurationExtensions
{
    public static GatewayOptions AddGatewayConfiguration(this WebApplicationBuilder builder)
    {
        builder.Configuration
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
            .AddJsonFile("gateway.runtime.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

        var isDevelopment = builder.Environment.IsDevelopment();
        var gatewayOptions = builder.Configuration.GetSection(GatewayOptions.SectionName).Get<GatewayOptions>() ?? new();

        builder.Services.AddOptions<GatewayOptions>()
            .Bind(builder.Configuration.GetSection(GatewayOptions.SectionName))
            .Validate(x => x.IsValid(isDevelopment), "Invalid gateway options; Mock and plaintext upstreams are Development-only.")
            .ValidateOnStart();

        return gatewayOptions;
    }
}
