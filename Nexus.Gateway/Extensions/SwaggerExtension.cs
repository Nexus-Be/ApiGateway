namespace Nexus.Gateway.Extensions;

using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Nexus.Gateway.MockEngine;
using Nexus.Gateway.Security;
using Nexus.Gateway.Swagger;

public static class SwaggerExtension
{
    private static readonly (string DocName, string Title, int Port, string Description)[] ServiceDocs =
    [
        ("users", "User & Auth Service", 8001, "Quản lý người dùng, hồ sơ, xác thực và phân quyền."),
        ("catalog", "Catalog Service", 8002, "Quản lý danh mục, sản phẩm và tồn kho ảo."),
        ("commerce", "Commerce Service", 8003, "Quản lý giỏ hàng và đơn đặt hàng."),
        ("log", "Log Service", 8004, "Nhật ký hệ thống và audit trail."),
        ("auction", "Auction Service", 8005, "Đấu giá và phiên đặt giá trực tuyến."),
        ("fulfillment", "Fulfillment Service", 8006, "Kho bãi vật lý và vận đơn giao vận."),
        ("notification", "Notification Service", 8007, "Thông báo email và in-app."),
        ("wallet", "Wallet Service", 8008, "Ví số dư, nạp rút và thanh toán giao dịch."),
        ("checkheal", "Health & CheckHeal", 8009, "Kiểm tra sức khỏe hệ thống và điều khiển Gateway.")
    ];

    private const string GlobalNavigationMarkdown = """

        ### 🌐 Fast Service Navigation

        | STT | Dịch Vụ | Port | Swagger Gateway (Mock & Auth) | Swagger Microservice Trực Tiếp |
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

        > Bạn có thể chuyển đổi nhanh giữa các tài liệu bằng menu dropdown **Select a spec** ở góc trên cùng bên phải.
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
                    API Gateway cho 8 microservices (catalog, commerce, fulfillment, users, auction, notification, log, wallet) và CheckHeal.

                    Hướng dẫn test độc lập & đổi chế độ trên Swagger UI:
                    - Đổi chế độ xử lý: Sử dụng header 'X-Gateway-Mode' (chọn 'Mock' để lấy dữ liệu seed, hoặc 'Real' để gọi backend service thật).
                    - Đổi chế độ xác thực: Sử dụng header 'X-Gateway-Auth' (chọn 'Bypass' để test mọi API không cần token, hoặc 'Enforce' để yêu cầu JWT thật).
                    - Đổi vai trò test (RBAC): Sử dụng header 'X-Mock-Role' (chọn 'buyer', 'seller', 'staff', 'admin') hoặc bấm nút 'Authorize' 🔓 để thiết lập toàn cục.
                    - Mọi header trên đều có sẵn dropdown trên từng API endpoint, thay đổi giá trị sẽ được gửi tự động khi bấm 'Execute'!
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
                        Tài liệu API nhóm {title} (Microservice Backend lắng nghe tại cổng {port}).
                        {desc}

                        Các API bên dưới được gọi thông qua Edge API Gateway (Port 8080) với đầy đủ cơ chế Mock Engine, Rate Limiting và Security.
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

            o.CustomSchemaIds(type => type.FullName?.Replace('+', '.') ?? type.Name);
            o.OperationFilter<GatewayHeaderOperationFilter>();
            o.DocumentFilter<MockEngineDocumentFilter>();
        });

        return services;
    }
}
