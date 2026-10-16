using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Nexus.Gateway.Extensions;

var builder = WebApplication.CreateBuilder(args);

// 1. Logging & Kestrel limits
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 64 * 1024);

// 2. Configuration & GatewayOptions
var gatewayOptions = builder.AddGatewayConfiguration();

// 3. Dependency Injection Services
builder.Services.AddGatewayCoreServices(builder.Configuration, builder.Environment, gatewayOptions);
builder.Services.AddGatewayGrpcClients();
builder.Services.AddGatewayTelemetry(builder.Configuration);
builder.Services.AddGatewaySwagger();

var app = builder.Build();

// 4. Middleware Pipeline
app.UseGatewayPipeline(app.Environment);

// 5. Standard Controllers (/gateway/api/v1/{service}/...)
app.MapControllers();

app.Run();

public partial class Program;
