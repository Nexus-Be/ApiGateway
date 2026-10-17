namespace Nexus.Gateway.Extensions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Nexus.Gateway.MockEngine;

public static class SwaggerExtension
{
    public static IServiceCollection AddGatewaySwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(o =>
        {
            o.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Nexus API Gateway",
                Version = "v1",
                Description = """
                    API Gateway cho 8 microservices (catalog, commerce, fulfillment, users, auction, notification, log, wallet).

                    Hướng dẫn test độc lập trên Swagger UI:
                    - Tiền tố API chuẩn: /api/v1/{service}/... (đường dẫn cũ /gateway/api/v1/... vẫn được hỗ trợ tương thích).
                    - Test độc lập không cần đăng nhập: Khi service ở chế độ Mock, bạn có thể click 'Try it out' -> 'Execute' trực tiếp trên bất kỳ API nào mà không cần nhập token!
                    - Test phân quyền RBAC: Bấm nút 'Authorize' màu xanh ở góc trên và nhập vai trò: 'admin', 'seller', 'staff', hoặc 'buyer'.
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

        return services;
    }
}
