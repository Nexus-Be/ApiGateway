namespace Nexus.Gateway.Extensions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Nexus.Gateway.MockEngine;
using Nexus.Gateway.Swagger;

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

                    Hướng dẫn test độc lập & đổi chế độ trên Swagger UI:
                    - Đổi chế độ xử lý: Sử dụng header 'X-Gateway-Mode' (chọn 'Mock' để lấy dữ liệu seed, hoặc 'Real' để gọi backend service thật).
                    - Đổi chế độ xác thực: Sử dụng header 'X-Gateway-Auth' (chọn 'Bypass' để test mọi API không cần token, hoặc 'Enforce' để yêu cầu JWT thật).
                    - Đổi vai trò test (RBAC): Sử dụng header 'X-Mock-Role' (chọn 'buyer', 'seller', 'staff', 'admin') hoặc bấm nút 'Authorize' 🔓 để thiết lập toàn cục.
                    - Mọi header trên đều có sẵn dropdown trên từng API endpoint, thay đổi giá trị sẽ được gửi tự động khi bấm 'Execute'!
                    """
            });

            o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Nhập JWT Bearer token thật HOẶC nhập tên vai trò giả lập ('admin', 'seller', 'staff', 'buyer') để test phân quyền."
            });

            o.AddSecurityDefinition("X-Gateway-Auth", new OpenApiSecurityScheme
            {
                Name = "X-Gateway-Auth",
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Description = "Chế độ xác thực toàn cục: 'Bypass' (không cần login khi test Mock) hoặc 'Enforce' (bắt buộc JWT)."
            });

            o.AddSecurityDefinition("X-Gateway-Mode", new OpenApiSecurityScheme
            {
                Name = "X-Gateway-Mode",
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Description = "Chế độ xử lý toàn cục: 'Mock' (lấy dữ liệu mock/seed) hoặc 'Real' (chuyển tiếp backend)."
            });

            o.AddSecurityDefinition("X-Mock-Role", new OpenApiSecurityScheme
            {
                Name = "X-Mock-Role",
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Description = "Vai trò giả lập toàn cục khi ở chế độ Bypass: 'buyer', 'seller', 'staff', 'admin'."
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

            o.OperationFilter<GatewayHeaderOperationFilter>();
            o.DocumentFilter<MockEngineDocumentFilter>();
        });

        return services;
    }
}
