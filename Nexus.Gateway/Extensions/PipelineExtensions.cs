namespace Nexus.Gateway.Extensions;

using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Nexus.Gateway.Middleware;
using Nexus.Gateway.MockEngine;
using Nexus.Gateway.Responses;
using Nexus.Gateway.Security;
using Nexus.Gateway.Services;
using Serilog;

public static class PipelineExtensions
{
    public static WebApplication UseGatewayPipeline(this WebApplication app, IWebHostEnvironment environment)
    {
        var isDevelopment = environment.IsDevelopment();

        app.UseExceptionHandler();
        app.UseStatusCodePages();

        app.UseSerilogRequestLogging(opts =>
        {
            opts.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                var traceId = ApiResponseFactory.ResolveTraceId(httpContext);
                diagnosticContext.Set("TraceId", traceId);
                if (httpContext.Items["BackendTag"] is string backend)
                {
                    diagnosticContext.Set("BackendTag", backend);
                }
                if (httpContext.Items["GatewayMode"] is string mode)
                {
                    diagnosticContext.Set("GatewayMode", mode);
                }
            };
        });

        // Custom Gateway Headers: X-Trace-Id, X-Gateway-Mode, X-Gateway-Auth, X-Content-Type-Options
        app.Use(async (context, next) =>
        {
            var traceId = ApiResponseFactory.ResolveTraceId(context);
            context.Response.OnStarting(() =>
            {
                context.Response.Headers["X-Trace-Id"] = traceId;
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";

                // 1. Phản hồi X-Gateway-Mode hiện tại
                var mode = context.Items["GatewayMode"] as string;
                if (string.IsNullOrEmpty(mode))
                {
                    if (GatewayRequestResolution.Get(context) is { } res)
                    {
                        var opts = context.RequestServices.GetService<IOptionsMonitor<GatewayOptions>>()?.CurrentValue ?? new();
                        var rtm = context.RequestServices.GetService<IGatewayRuntimeManager>();
                        if (rtm != null)
                        {
                            mode = res.EffectiveMode(context, res.Service, opts, rtm);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(mode))
                {
                    context.Response.Headers["X-Gateway-Mode"] = mode;
                    if (string.Equals(mode, "Mock", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Response.Headers.CacheControl = "no-store";
                    }
                }

                // 2. Phản hồi X-Gateway-Auth hiện tại (Bypass / Enforce)
                var rtmgr = context.RequestServices.GetService<IGatewayRuntimeManager>();
                if (rtmgr != null)
                {
                    var isBypassed = rtmgr.IsAuthBypassed(context);
                    context.Response.Headers["X-Gateway-Auth"] = isBypassed ? "Bypass" : "Enforce";
                }

                return Task.CompletedTask;
            });

            await next(context);
        });

        // Transparent rewrite for backward compatibility: /gateway/api/* -> /api/*
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/gateway/api", out var remaining))
            {
                context.Request.Path = "/api" + remaining;
            }
            await next(context);
        });

        app.UseRouting();
        app.UseCors();
        // Resolve once after the legacy rewrite and routing, before any auth decision.
        app.Use(async (context, next) =>
        {
            GatewayRequestResolution.Resolve(
                context,
                context.RequestServices.GetRequiredService<IMockEngine>(),
                context.RequestServices.GetRequiredService<IOptionsMonitor<GatewayOptions>>(),
                context.RequestServices.GetRequiredService<IGatewayRuntimeManager>());
            await next(context);
        });
        app.UseMockSecurityAdapter();
        app.UseAuthentication();
        app.UseRedisRateLimit();
        app.UseRateLimiter();
        app.UseAuthorization();
        app.UseMockEngine();
        app.UseRequestTimeouts();

        if (isDevelopment)
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "00. Nexus Gateway (All APIs)");
                c.SwaggerEndpoint("/swagger/users/swagger.json", "01. User & Auth Service (8001)");
                c.SwaggerEndpoint("/swagger/catalog/swagger.json", "02. Catalog Service (8002)");
                c.SwaggerEndpoint("/swagger/commerce/swagger.json", "03. Commerce Service (8003)");
                c.SwaggerEndpoint("/swagger/log/swagger.json", "04. Log Service (8004)");
                c.SwaggerEndpoint("/swagger/auction/swagger.json", "05. Auction Service (8005)");
                c.SwaggerEndpoint("/swagger/fulfillment/swagger.json", "06. Fulfillment Service (8006)");
                c.SwaggerEndpoint("/swagger/notification/swagger.json", "07. Notification Service (8007)");
                c.SwaggerEndpoint("/swagger/wallet/swagger.json", "08. Wallet Service (8008)");
                c.SwaggerEndpoint("/swagger/checkheal/swagger.json", "09. Health & CheckHeal (8009)");

                // Downstream microservices raw swagger specifications (proxied via Gateway)
                c.SwaggerEndpoint("/swagger-downstream/users/swagger.json", "Backend Raw: User Service (8001)");
                c.SwaggerEndpoint("/swagger-downstream/catalog/swagger.json", "Backend Raw: Catalog (8002)");
                c.SwaggerEndpoint("/swagger-downstream/commerce/swagger.json", "Backend Raw: Commerce (8003)");
                c.SwaggerEndpoint("/swagger-downstream/log/swagger.json", "Backend Raw: Log (8004)");
                c.SwaggerEndpoint("/swagger-downstream/auction/swagger.json", "Backend Raw: Auction (8005)");
                c.SwaggerEndpoint("/swagger-downstream/fulfillment/swagger.json", "Backend Raw: Fulfillment (8006)");
                c.SwaggerEndpoint("/swagger-downstream/notification/swagger.json", "Backend Raw: Notification (8007)");
                c.SwaggerEndpoint("/swagger-downstream/wallet/swagger.json", "Backend Raw: Wallet (8008)");
                c.SwaggerEndpoint("/swagger-downstream/checkheal/swagger.json", "Backend Raw: CheckHeal (8009)");

                c.EnablePersistAuthorization();
                c.DisplayRequestDuration();
            });
            app.MapGet("/", () => Results.Redirect("/swagger"));

            app.MapGet("/swagger-downstream/{service}/swagger.json", async (
                string service,
                IOptionsMonitor<GatewayOptions> optionsMonitor,
                IHttpClientFactory httpClientFactory,
                CancellationToken ct) =>
            {
                var options = optionsMonitor.CurrentValue;
                var backend = options.GetService(service);
                if (backend == null || string.IsNullOrWhiteSpace(backend.Address))
                {
                    return Results.NotFound(new { error = $"Service '{service}' is not registered in Gateway." });
                }

                var downstreamUrl = $"{backend.Address.TrimEnd('/')}/swagger/v1/swagger.json";
                var client = httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(3);

                try
                {
                    var response = await client.GetAsync(downstreamUrl, ct);
                    if (!response.IsSuccessStatusCode)
                    {
                        return Results.StatusCode((int)response.StatusCode);
                    }

                    var content = await response.Content.ReadAsStringAsync(ct);
                    return Results.Content(content, "application/json");
                }
                catch (Exception ex)
                {
                    return Results.Problem(
                        title: $"Downstream service '{service}' is offline or unreachable.",
                        detail: $"Failed to reach {downstreamUrl}. {ex.Message}",
                        statusCode: StatusCodes.Status503ServiceUnavailable);
                }
            }).ExcludeFromDescription();
        }

        app.MapHealthChecks("/health/live");
        app.MapGet("/health/ready", (IOptionsMonitor<GatewayOptions> o) =>
            o.CurrentValue.IsValid(isDevelopment) ? Results.Ok(new { status = "ready" }) : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

        return app;
    }
}
