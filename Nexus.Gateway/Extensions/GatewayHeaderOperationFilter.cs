using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Nexus.Gateway.Swagger;

/// <summary>
/// Automatically injects Gateway control headers into every endpoint on Swagger UI:
/// - X-Gateway-Mode: Switch between 'Mock' and 'Real' modes.
/// - X-Gateway-Auth: Switch between 'Bypass' (no token required) and 'Enforce' (JWT required).
/// - X-Mock-Role: Simulate user roles ('buyer', 'seller', 'staff', 'admin') in Bypass mode.
/// </summary>
public sealed class GatewayHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= new List<OpenApiParameter>();

        // 1. X-Gateway-Mode: Allows switching Mock / Real mode directly on each request
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Gateway-Mode",
            In = ParameterLocation.Header,
            Required = false,
            Description = "Gateway dispatch mode: 'Mock' (serve static mock/seed fixtures) or 'Real' (forward to backend RESTful microservice). Default: 'Mock'.",
            Schema = new OpenApiSchema
            {
                Type = "string",
                Enum = new List<IOpenApiAny>
                {
                    new OpenApiString("Mock"),
                    new OpenApiString("Real")
                },
                Default = new OpenApiString("Mock")
            }
        });

        // 2. X-Gateway-Auth: Allows testing without authentication or enforcing valid JWT
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Gateway-Auth",
            In = ParameterLocation.Header,
            Required = false,
            Description = "Authentication mode: 'Bypass' (bypass authentication in mock mode) or 'Enforce' (enforce valid JWT). Default: 'Bypass'.",
            Schema = new OpenApiSchema
            {
                Type = "string",
                Enum = new List<IOpenApiAny>
                {
                    new OpenApiString("Bypass"),
                    new OpenApiString("Enforce")
                },
                Default = new OpenApiString("Bypass")
            }
        });

        // 3. X-Mock-Role: Allows changing simulated role in testing
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Mock-Role",
            In = ParameterLocation.Header,
            Required = false,
            Description = "Simulated user role when running in Mock/Bypass mode: 'buyer', 'seller', 'staff', 'admin'. Default: 'buyer'.",
            Schema = new OpenApiSchema
            {
                Type = "string",
                Enum = new List<IOpenApiAny>
                {
                    new OpenApiString("buyer"),
                    new OpenApiString("seller"),
                    new OpenApiString("staff"),
                    new OpenApiString("admin")
                },
                Default = new OpenApiString("buyer")
            }
        });
    }
}
