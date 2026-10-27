using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Nexus.Gateway.Swagger;

/// <summary>
/// Tự động thêm các header điều khiển Gateway vào từng endpoint trên Swagger UI:
/// - X-Gateway-Mode: Chuyển đổi giữa chế độ 'Mock' và 'Real'.
/// - X-Gateway-Auth: Chuyển đổi giữa xác thực 'Bypass' (không cần login) và 'Enforce' (bắt buộc JWT).
/// - X-Mock-Role: Giả lập vai trò ('buyer', 'seller', 'staff', 'admin') khi chạy chế độ Bypass.
/// </summary>
public sealed class GatewayHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= new List<OpenApiParameter>();

        // 1. X-Gateway-Mode: Cho phép đổi chế độ Mock / Real trực tiếp trên từng request
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Gateway-Mode",
            In = ParameterLocation.Header,
            Required = false,
            Description = "Chế độ xử lý của Gateway: 'Mock' (lấy dữ liệu mock/seed) hoặc 'Real' (chuyển tiếp tới backend RESTful microservice). Mặc định: 'Mock'.",
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

        // 2. X-Gateway-Auth: Cho phép test không cần đăng nhập hoặc ép kiểm tra JWT thật
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Gateway-Auth",
            In = ParameterLocation.Header,
            Required = false,
            Description = "Chế độ xác thực: 'Bypass' (không cần login khi test Mock) hoặc 'Enforce' (bắt buộc JWT hợp lệ). Mặc định: 'Bypass'.",
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

        // 3. X-Mock-Role: Cho phép đổi vai trò giả lập khi test
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Mock-Role",
            In = ParameterLocation.Header,
            Required = false,
            Description = "Vai trò giả lập khi ở chế độ Mock/Bypass: 'buyer', 'seller', 'staff', 'admin'. Mặc định: 'buyer'.",
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
