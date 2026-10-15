using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Nexus.Gateway;
using Nexus.Gateway.Middleware;
using Nexus.Gateway.Responses;
using Nexus.Gateway.Security;
using Nexus.Gateway.MockEngine;
using Nexus.Gateway.Services;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ShoppingCart.Contracts.V1;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddJsonFile("gateway.runtime.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

var development = builder.Environment.IsDevelopment();
var gateway = builder.Configuration.GetSection(GatewayOptions.SectionName).Get<GatewayOptions>() ?? new();

builder.Services.AddOptions<GatewayOptions>()
    .Bind(builder.Configuration.GetSection(GatewayOptions.SectionName))
    .Validate(x => x.IsValid(development), "Invalid gateway options; Mock and plaintext upstreams are Development-only.")
    .ValidateOnStart();

builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 64 * 1024);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IApiResponseFactory, ApiResponseFactory>();
builder.Services.AddSingleton<IGatewayRuntimeManager, GatewayRuntimeManager>();
builder.Services.AddSingleton<IMockEngine, MockEngine>();
builder.Services.AddSingleton<BackendDispatcher>();
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var redisOptions = ConfigurationOptions.Parse(gateway.RedisConnectionString);
    redisOptions.AbortOnConnectFail = false;
    redisOptions.ConnectRetry = 1;
    return ConnectionMultiplexer.Connect(redisOptions);
});
builder.Services.AddSingleton<IDistributedRateLimitStore, RedisRateLimitStore>();

builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = c =>
    c.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? c.HttpContext.TraceIdentifier);

builder.Services.AddExceptionHandler<GatewayExceptionHandler>();

builder.Services.AddGatewaySecurity(builder.Configuration, development);
builder.Services.AddGatewayCors(gateway);
builder.Services.AddGatewayRateLimiter(gateway);

builder.Services.AddRequestTimeouts(o => o.DefaultPolicy = new RequestTimeoutPolicy
{
    Timeout = TimeSpan.FromSeconds(65),
    TimeoutStatusCode = StatusCodes.Status504GatewayTimeout
});

builder.Services.AddHealthChecks();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("nexus-gateway"))
    .WithTracing(t =>
    {
        t.AddAspNetCoreInstrumentation()
         .AddHttpClientInstrumentation()
         .AddSource(BackendDispatcher.ActivityName)
         .AddSource("Grpc.Net.Client");

        if (!string.IsNullOrEmpty(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            t.AddOtlpExporter();
        }
    })
    .WithMetrics(m =>
    {
        m.AddAspNetCoreInstrumentation()
         .AddHttpClientInstrumentation()
         .AddMeter("Microsoft.AspNetCore.RateLimiting");

        if (!string.IsNullOrEmpty(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            m.AddOtlpExporter();
        }
    });

builder.Services.AddGrpcClient<CatalogApi.CatalogApiClient>((sp, o) =>
{
    var opts = sp.GetRequiredService<IOptionsMonitor<GatewayOptions>>().CurrentValue;
    var svc = opts.GetService("catalog");
    if (svc != null && !string.IsNullOrEmpty(svc.Address))
        o.Address = new Uri(svc.Address);
});

builder.Services.AddGrpcClient<CommerceApi.CommerceApiClient>((sp, o) =>
{
    var opts = sp.GetRequiredService<IOptionsMonitor<GatewayOptions>>().CurrentValue;
    var svc = opts.GetService("commerce");
    if (svc != null && !string.IsNullOrEmpty(svc.Address))
        o.Address = new Uri(svc.Address);
});

builder.Services.AddGrpcClient<UsersApi.UsersApiClient>((sp, o) =>
{
    var opts = sp.GetRequiredService<IOptionsMonitor<GatewayOptions>>().CurrentValue;
    var svc = opts.GetService("users");
    if (svc != null && !string.IsNullOrEmpty(svc.Address))
        o.Address = new Uri(svc.Address);
});

builder.Services.AddGrpcClient<AuditApi.AuditApiClient>((sp, o) =>
{
    var opts = sp.GetRequiredService<IOptionsMonitor<GatewayOptions>>().CurrentValue;
    var svc = opts.GetService("audit");
    if (svc != null && !string.IsNullOrEmpty(svc.Address))
        o.Address = new Uri(svc.Address);
});

builder.Services.AddGrpcClient<AuctionApi.AuctionApiClient>((sp, o) =>
{
    var opts = sp.GetRequiredService<IOptionsMonitor<GatewayOptions>>().CurrentValue;
    var svc = opts.GetService("auction");
    if (svc != null && !string.IsNullOrEmpty(svc.Address))
        o.Address = new Uri(svc.Address);
});

builder.Services.AddGrpcClient<FulfillmentApi.FulfillmentApiClient>((sp, o) =>
{
    var opts = sp.GetRequiredService<IOptionsMonitor<GatewayOptions>>().CurrentValue;
    var svc = opts.GetService("fulfillment");
    if (svc != null && !string.IsNullOrEmpty(svc.Address))
        o.Address = new Uri(svc.Address);
});

builder.Services.AddGrpcClient<NotificationApi.NotificationApiClient>((sp, o) =>
{
    var opts = sp.GetRequiredService<IOptionsMonitor<GatewayOptions>>().CurrentValue;
    var svc = opts.GetService("notification");
    if (svc != null && !string.IsNullOrEmpty(svc.Address))
        o.Address = new Uri(svc.Address);
});

builder.Services.AddGrpcClient<WalletApi.WalletApiClient>((sp, o) =>
{
    var opts = sp.GetRequiredService<IOptionsMonitor<GatewayOptions>>().CurrentValue;
    var svc = opts.GetService("wallet");
    if (svc != null && !string.IsNullOrEmpty(svc.Address))
        o.Address = new Uri(svc.Address);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Nexus Gateway",
        Version = "v1",
        Description = """
            API Gateway cho 8 microservices (catalog, commerce, fulfillment, users, auction, notification, audit, wallet).

            💡 Hướng dẫn test độc lập trên Swagger UI:
            - Test độc lập không cần đăng nhập: Khi service ở chế độ Mock, bạn có thể click 'Try it out' -> 'Execute' trực tiếp trên bất kỳ API nào (kể cả GET /users/me, giỏ hàng) mà không cần nhập token!
            - Test phân quyền RBAC: Bấm nút 'Authorize' màu xanh ở góc trên và nhập: 'admin', 'seller', 'staff', hoặc 'buyer'.
            """
    });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        }] = Array.Empty<string>()
    });
    o.DocumentFilter<MockEngineDocumentFilter>();
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.Use(async (context, next) =>
{
    var traceId = ApiResponseFactory.ResolveTraceId(context);
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Trace-Id"] = traceId;
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        if (context.Items["GatewayMode"] is string mode)
        {
            context.Response.Headers["X-Gateway-Mode"] = mode;
            if (mode == "Mock")
            {
                context.Response.Headers.CacheControl = "no-store";
            }
        }
        return Task.CompletedTask;
    });

    await next(context);
});

app.UseRouting();
app.UseCors();
app.UseMockSecurityAdapter();
app.UseAuthentication();
app.UseRedisRateLimit();
app.UseRateLimiter();
app.UseAuthorization();
app.UseMockEngine();
app.UseRequestTimeouts();

if (development)
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapGet("/", () => Results.Redirect("/swagger"));
}

app.MapHealthChecks("/health/live");
app.MapGet("/health/ready", (IOptionsMonitor<GatewayOptions> o) =>
    o.CurrentValue.IsValid(development) ? Results.Ok(new { status = "ready" }) : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

GatewayEndpoints.Map(app);

app.Run();

public partial class Program;
