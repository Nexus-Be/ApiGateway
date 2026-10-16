namespace Nexus.Gateway.Extensions;

using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nexus.Gateway.Middleware;
using Nexus.Gateway.MockEngine;
using Nexus.Gateway.Responses;
using Nexus.Gateway.Security;
using Nexus.Gateway.Services;
using StackExchange.Redis;

public static class CoreServicesExtension
{
    public static IServiceCollection AddGatewayCoreServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        GatewayOptions gatewayOptions)
    {
        var isDevelopment = environment.IsDevelopment();

        services.AddHttpContextAccessor();
        services.AddControllers();
        services.AddSingleton<IApiResponseFactory, ApiResponseFactory>();
        services.AddSingleton<IGatewayRuntimeManager, GatewayRuntimeManager>();
        services.AddSingleton<IMockEngine, MockEngine>();
        services.AddSingleton<Dispatching.IModeEvaluator, Dispatching.ModeEvaluator>();
        services.AddSingleton<Dispatching.IMockDataMapper, Dispatching.MockDataMapper>();
        services.AddSingleton<Dispatching.IGrpcServiceCaller, Dispatching.GrpcServiceCaller>();
        services.AddSingleton<Dispatching.IGatewayDispatcher, Dispatching.GatewayDispatcher>();
        services.AddSingleton<BackendDispatcher>();

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var redisOptions = ConfigurationOptions.Parse(gatewayOptions.RedisConnectionString);
            redisOptions.AbortOnConnectFail = false;
            redisOptions.ConnectRetry = 1;
            return ConnectionMultiplexer.Connect(redisOptions);
        });
        services.AddSingleton<IDistributedRateLimitStore, RedisRateLimitStore>();

        services.AddProblemDetails(o => o.CustomizeProblemDetails = c =>
            c.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? c.HttpContext.TraceIdentifier);

        services.AddExceptionHandler<GatewayExceptionHandler>();

        services.AddGatewaySecurity(configuration, isDevelopment);
        services.AddGatewayCors(gatewayOptions);
        services.AddGatewayRateLimiter(gatewayOptions);

        services.AddRequestTimeouts(o => o.DefaultPolicy = new RequestTimeoutPolicy
        {
            Timeout = TimeSpan.FromSeconds(65),
            TimeoutStatusCode = StatusCodes.Status504GatewayTimeout
        });

        services.AddHealthChecks();

        return services;
    }
}
