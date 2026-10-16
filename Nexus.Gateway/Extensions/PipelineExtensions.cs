namespace Nexus.Gateway.Extensions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Nexus.Gateway.Middleware;
using Nexus.Gateway.MockEngine;
using Nexus.Gateway.Responses;
using Nexus.Gateway.Security;

public static class PipelineExtensions
{
    public static WebApplication UseGatewayPipeline(this WebApplication app, IWebHostEnvironment environment)
    {
        var isDevelopment = environment.IsDevelopment();

        app.UseExceptionHandler();
        app.UseStatusCodePages();

        // Custom Gateway Headers: X-Trace-Id, X-Gateway-Mode, X-Content-Type-Options
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

        if (isDevelopment)
        {
            app.UseSwagger();
            app.UseSwaggerUI();
            app.MapGet("/", () => Results.Redirect("/swagger"));
        }

        app.MapHealthChecks("/health/live");
        app.MapGet("/health/ready", (IOptionsMonitor<GatewayOptions> o) =>
            o.CurrentValue.IsValid(isDevelopment) ? Results.Ok(new { status = "ready" }) : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

        return app;
    }
}
