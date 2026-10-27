namespace Nexus.Gateway.Extensions;

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
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Nexus API Gateway v1");
                c.EnablePersistAuthorization();
                c.DisplayRequestDuration();
            });
            app.MapGet("/", () => Results.Redirect("/swagger"));
        }

        app.MapHealthChecks("/health/live");
        app.MapGet("/health/ready", (IOptionsMonitor<GatewayOptions> o) =>
            o.CurrentValue.IsValid(isDevelopment) ? Results.Ok(new { status = "ready" }) : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

        return app;
    }
}
