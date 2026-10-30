namespace Nexus.Gateway.Extensions;

using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Nexus.Gateway.Security;
using Nexus.Gateway.Swagger;

public static class SwaggerExtensions
{
    private static readonly (string DocName, string Title, int Port, string Description)[] ServiceDocs =
    [
        ("users", "User & Auth Service", 8001, "User accounts, profile management, authentication, and authorization."),
        ("catalog", "Catalog Service", 8002, "Categories, products, and virtual inventory management."),
        ("commerce", "Commerce Service", 8003, "Shopping carts and order management."),
        ("log", "Log Service", 8004, "Centralized system logs and audit trail."),
        ("auction", "Auction Service", 8005, "Online auctions and real-time bidding sessions."),
        ("fulfillment", "Fulfillment Service", 8006, "Physical warehouses, parcels, and shipment tracking."),
        ("notification", "Notification Service", 8007, "Email and in-app notifications."),
        ("wallet", "Wallet Service", 8008, "Wallet balance, deposits, withdrawals, and payments."),
        ("checkheal", "Health & CheckHeal", 8009, "System health monitoring and Gateway keep-alive prober.")
    ];

    private const string GlobalNavigationMarkdown = """

        ### 🌐 Fast Service Navigation

        | # | Service | Port | Gateway Swagger (Mock & Auth) | Direct Microservice Swagger |
        |:---:|:---|:---:|:---|:---|
        | 00 | **Nexus API Gateway (All APIs)** | `8080` | `/swagger/v1/swagger.json` | [http://localhost:8080/swagger](http://localhost:8080/swagger) |
        | 01 | **User & Auth Service** | `8001` | `/swagger/users/swagger.json` | [http://localhost:8001/swagger](http://localhost:8001/swagger) |
        | 02 | **Catalog Service** | `8002` | `/swagger/catalog/swagger.json` | [http://localhost:8002/swagger](http://localhost:8002/swagger) |
        | 03 | **Commerce Service** | `8003` | `/swagger/commerce/swagger.json` | [http://localhost:8003/swagger](http://localhost:8003/swagger) |
        | 04 | **Log Service** | `8004` | `/swagger/log/swagger.json` | [http://localhost:8004/swagger](http://localhost:8004/swagger) |
        | 05 | **Auction Service** | `8005` | `/swagger/auction/swagger.json` | [http://localhost:8005/swagger](http://localhost:8005/swagger) |
        | 06 | **Fulfillment Service** | `8006` | `/swagger/fulfillment/swagger.json` | [http://localhost:8006/swagger](http://localhost:8006/swagger) |
        | 07 | **Notification Service** | `8007` | `/swagger/notification/swagger.json` | [http://localhost:8007/swagger](http://localhost:8007/swagger) |
        | 08 | **Wallet Service** | `8008` | `/swagger/wallet/swagger.json` | [http://localhost:8008/swagger](http://localhost:8008/swagger) |
        | 09 | **Health & CheckHeal** | `8009` | `/swagger/checkheal/swagger.json` | [http://localhost:8009/swagger](http://localhost:8009/swagger) |

        > Quickly switch between service specifications using the **Select a spec** dropdown in the top-right corner.
        """;

    public static IServiceCollection AddGatewaySwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(o =>
        {
            o.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Nexus API Gateway (All APIs)",
                Version = "v1",
                Description = $"""
                    Edge API Gateway for 8 microservices (catalog, commerce, fulfillment, users, auction, notification, log, wallet) and CheckHeal.

                    Testing guide on Swagger UI:
                    - Dispatch mode: Use 'X-Gateway-Mode' header ('Mock' to serve seed data, or 'Real' to call live backend services).
                    - Authentication mode: Use 'X-Gateway-Auth' header ('Bypass' to test without tokens, or 'Enforce' to require valid JWT).
                    - Role-based testing (RBAC): Use 'X-Mock-Role' header ('buyer', 'seller', 'staff', 'admin') or the 'Authorize' button.
                    - All headers are available in endpoint parameter dropdowns.
                    {GlobalNavigationMarkdown}
                    """
            });

            foreach (var (docName, title, port, desc) in ServiceDocs)
            {
                o.SwaggerDoc(docName, new OpenApiInfo
                {
                    Title = $"Nexus Gateway - {title} (Port {port})",
                    Version = docName,
                    Description = $"""
                        API documentation for {title} (listening directly on port {port}).
                        {desc}

                        Endpoints below are routed through Edge API Gateway (port 8080) with Mock Engine, Rate Limiting, and Security.
                        {GlobalNavigationMarkdown}
                        """
                });
            }

            o.DocInclusionPredicate((docName, apiDesc) =>
            {
                if (string.Equals(docName, "v1", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (apiDesc.ActionDescriptor is ControllerActionDescriptor cad)
                {
                    var tag = cad.ControllerTypeInfo.GetCustomAttributes(typeof(BackendTagAttribute), true)
                        .OfType<BackendTagAttribute>()
                        .FirstOrDefault()?.Name;

                    if (tag != null && string.Equals(tag, docName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            });

            o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Enter a valid JWT Bearer token or a simulated role ('admin', 'seller', 'staff', 'buyer') to test authorization."
            });

            o.AddSecurityDefinition("X-Gateway-Auth", new OpenApiSecurityScheme
            {
                Name = "X-Gateway-Auth",
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Description = "Global authentication mode: 'Bypass' (bypass token validation in mock testing) or 'Enforce' (require valid JWT)."
            });

            o.AddSecurityDefinition("X-Gateway-Mode", new OpenApiSecurityScheme
            {
                Name = "X-Gateway-Mode",
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Description = "Global dispatch mode: 'Mock' (serve static mock/seed fixtures) or 'Real' (forward to backend microservice)."
            });

            o.AddSecurityDefinition("X-Mock-Role", new OpenApiSecurityScheme
            {
                Name = "X-Mock-Role",
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Description = "Simulated user role when in Bypass mode: 'buyer', 'seller', 'staff', 'admin'."
            });

            o.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                }] = Array.Empty<string>(),
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "X-Gateway-Auth" }
                }] = Array.Empty<string>(),
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "X-Gateway-Mode" }
                }] = Array.Empty<string>(),
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "X-Mock-Role" }
                }] = Array.Empty<string>()
            });

            o.CustomSchemaIds(type => type.FullName?.Replace('+', '.') ?? type.Name);
            o.OperationFilter<GatewayHeaderOperationFilter>();
        });

        return services;
    }
}
