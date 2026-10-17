# Hướng Dẫn Kiến Trúc & Thiết Lập Các Microservices (SETUP_GUIDE_SERVICES.md)

Tài liệu này cung cấp toàn bộ quy trình thiết lập, chuẩn giao tiếp và các bước cần thực hiện để sẵn sàng kết nối các backend microservices với **Nexus API Gateway**.

---

## 1. Bản Đồ Phân Bổ Cổng Mạng (Port Map) & Quy Hoạch 8 Services

Gateway làm đầu mối duy nhất cho Frontend gọi vào ở cổng `8000`. Toàn bộ 8 Microservices chạy độc lập từ cổng `8001` đến `8008`:

| STT | Tên Service | Port | Database | Nhiệm vụ chính | Trạng thái Gateway |
|:---:|:---|:---:|:---|:---|:---:|
| **0** | **Nexus.Gateway** | **8000** | *(Redis: 6379)* | Edge Proxy, JWT/RBAC, RateLimit, MockEngine, OpenTelemetry | ✅ Hoàn thành 100% |
| **1** | **Users Service** | **8001** | `usermanagement_db` (5432) | Đăng ký, đăng nhập, phát hành JWT, User Profile, RBAC, Duyệt Seller | Sẵn sàng kết nối |
| **2** | **Catalog Service** | **8002** | `category_db` (5433) | Quản lý danh mục, sản phẩm, tồn kho kho ảo của Seller | Sẵn sàng kết nối |
| **3** | **Commerce Service** | **8003** | `commerce_db` (5434) | Giỏ hàng, tạo đơn hàng, quản lý đơn hàng Seller/Admin, Điều phối Checkout Saga | Sẵn sàng kết nối |
| **4** | **Log Service** | **8004** | `log_db` (5435) | Lưu vết nhật ký tập trung và sự kiện qua RabbitMQ event consumer | Sẵn sàng kết nối |
| **5** | **Auction Service** | **8005** | `auction_db` (5436) | Phiên đấu giá, trả giá (bid), đồng hồ đếm ngược | Sẵn sàng kết nối |
| **6** | **Fulfillment Service**| **8006** | `fulfillment_db` (5437) | Tồn kho vật lý kho bãi, tính phí vận chuyển, tra cứu mã vận đơn | Sẵn sàng kết nối |
| **7** | **Notification Service**| **8007** | `notification_db` (5438)| Gửi thông báo, lịch sử thông báo, cài đặt thông báo người dùng | Sẵn sàng kết nối |
| **8** | **Wallet Service** | **8008** | `wallet_db` (5439) | Ví điện tử riêng biệt, số dư, nạp/rút tiền, chuyển tiền, thanh toán đơn hàng | Sẵn sàng kết nối |

---

## 2. Các Việc Đã Hoàn Thiện Tại Gateway

Gateway có các thành phần nền tảng sau; chạy lại `dotnet test ApiGateway.sln -c Release` để kiểm tra phiên bản hiện tại. Tests Gateway không chứng minh các backend đã triển khai/tích hợp đầy đủ:
1. **REST sang gRPC Translation**: Khai báo 8 gRPC Clients typed an toàn theo contract `shop.proto`.
2. **Mock Engine**: Hỗ trợ chạy Mock độc lập từng service mà không cần backend chạy thật.
3. **Cô lập trạng thái Real vs Mock theo từng Service**: Bạn có thể bật 1 service bất kỳ sang `Real` (ví dụ `catalog: Real`) để code và test, trong khi 7 service còn lại vẫn chạy `Mock` bình thường.
4. **Authentication & RBAC**: Kiểm tra JWT Token chặt chẽ (chặn mock token khi service ở chế độ Real). `AuthMode: Bypass` chỉ dành cho Development, request Mock đủ điều kiện và không áp dụng cho control-plane.
5. **IETF Standard Errors**: Trả mã lỗi Problem Details theo chuẩn quốc tế RFC 9110 & RFC 6585.
6. **Observability**: Tích hợp sẵn OpenTelemetry 1.18.0 Tracing và Metrics (hỗ trợ đẩy OTLP ra Jaeger/Prometheus).

---

## 3. Các Phần Còn Lại Cho Gateway (Nếu Muốn Mở Rộng Thêm Trong Tương Lai)

Cần hoàn thiện RPC, triển khai backend và test tích hợp cho từng service trước khi bật Real trong môi trường dùng chung. Những hướng mở rộng sau chưa được đảm bảo bởi Gateway hiện tại:
- **Output Caching**: Thêm cache Redis cho các endpoint đọc công khai ít thay đổi (ví dụ: `GET /api/v1/catalog/categories`).
- **Circuit Breaker / Retry Policy (Polly)**: Tự động ngắt mạch nếu một service backend gRPC bị sập liên tục quá nhiều lần.
- **BFF (Backend for Frontend)**: Ghép dữ liệu đa nguồn nếu một màn hình mobile cần gộp thông tin từ cả Catalog, Commerce và Wallet.

---

## 4. Các Bước Thiết Lập Cho Từng Microservice Backend

Khi bắt tay vào code hoặc hoàn thiện từng service backend (`Users`, `Catalog`, `Commerce`, `Wallet`...), hãy làm theo các bước chuẩn sau:

### Bước 1: Hạ Tầng Database & RabbitMQ (Docker Compose)
Chạy docker-compose để dựng toàn bộ DB và RabbitMQ:
```bash
docker compose up -d
```
Kiểm tra RabbitMQ Management UI tại: `http://localhost:15672` (user/password theo file `.env`).

### Bước 2: Đồng Bộ Proto Contract
File `Contracts/shop.proto` tại `Nexus.Gateway/Contracts/shop.proto` là **Nguồn sự thật duy nhất (Single Source of Truth)**.
- Khi tạo mới một service backend (ví dụ `WalletService`), copy file `shop.proto` này vào thư mục `Contracts/` của service đó.
- Đảm bảo file `.csproj` của service backend khai báo:
  ```xml
  <Protobuf Include="Contracts/shop.proto" GrpcServices="Server" />
  ```

### Bước 3: Implement gRPC Service Server
Trong service backend, kế thừa từ lớp base do gRPC sinh ra, ví dụ với `WalletService`:
```csharp
public class WalletGrpcService : WalletApi.WalletApiBase
{
    private readonly IWalletService _walletService;
    public WalletGrpcService(IWalletService walletService) => _walletService = walletService;

    public override async Task<Wallet> GetWallet(UserRequest request, ServerCallContext context)
    {
        var data = await _walletService.GetByUserIdAsync(request.UserId);
        return new Wallet
        {
            Id = data.Id.ToString(),
            UserId = data.UserId,
            Balance = data.Balance.ToString("0.00"),
            Currency = data.Currency,
            Status = data.Status
        };
    }
}
```
Và đăng ký trong `Program.cs` của service:
```csharp
builder.Services.AddGrpc();
// ...
app.MapGrpcService<WalletGrpcService>();
```

### Bước 4: Thiết Lập RabbitMQ & MassTransit Event Bus
Tất cả các service backend dùng phiên bản MassTransit đã chốt **8.4.1**:
```csharp
builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", 5672, "/", h =>
        {
            h.Username(builder.Configuration["RabbitMq:User"] ?? "guest");
            h.Password(builder.Configuration["RabbitMq:Password"] ?? "guest");
        });
        cfg.ConfigureEndpoints(context);
    });
});
```

### Bước 5: Thiết Lập Saga Điều Phối Thanh Toán (Checkout & Wallet Saga)
Khi tách riêng Ví điện tử khỏi Commerce, luồng Saga thanh toán diễn ra như sau:
```
1. Client gửi POST /api/v1/commerce/orders qua Gateway -> CommerceService tạo Order (Status = Pending).
2. CommerceService bắn event OrderSubmitted lên RabbitMQ.
3. CatalogService lắng nghe -> Giữ kho ảo (ReserveStock). Bắn StockReservedEvent.
4. WalletService lắng nghe -> Trừ tiền trong ví khách hàng (DeductBalance). Bắn PaymentCompletedEvent.
5. CommerceService nhận PaymentCompletedEvent -> Cập nhật Order (Status = Confirmed / Paid).
6. [BÙ TRỪ NẾU LỖI]: Nếu WalletService trừ tiền thất bại (không đủ tiền) -> Bắn PaymentFailedEvent -> CatalogService nhả kho (ReleaseStock) và CommerceService hủy đơn (OrderCancelled).
```
### Bước 6: Thiết Lập Distributed Tracing (OpenTelemetry) — Đúng 1 Dòng Code (Zero-Touch)
Để toàn bộ luồng gọi từ Gateway xuyên qua service của bạn được vẽ cây vết (Flame Graph) tự động trên Grafana Tempo:

1. **Thêm các Package NuGet sau vào file `.csproj` của service con**:
   ```xml
   <ItemGroup>
     <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.18.0" />
     <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.18.0" />
     <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.18.0" />
     <PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.18.0" />
     <PackageReference Include="OpenTelemetry.Instrumentation.EntityFrameworkCore" Version="1.0.0-beta.11" /> <!-- Nếu dùng EF Core -->
   </ItemGroup>
   ```

2. **Tạo file Extension dùng chung `ObservabilityExtensions.cs` trong service của bạn**:
   ```csharp
   using OpenTelemetry.Resources;
   using OpenTelemetry.Trace;

   namespace Common.Observability;

   public static class ObservabilityExtensions
   {
       public static IServiceCollection AddServiceObservability(this IServiceCollection services, IConfiguration configuration, string serviceName)
       {
           var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? configuration["Otel:Endpoint"] ?? "http://localhost:4317";

           services.AddOpenTelemetry()
               .ConfigureResource(r => r.AddService(serviceName))
               .WithTracing(t =>
               {
                   t.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSource("Grpc.Net.Client")
                    .AddSource("MassTransit"); // Tự động bắt event RabbitMQ

                   if (!string.IsNullOrEmpty(otlpEndpoint))
                   {
                       t.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
                   }
               });

           return services;
       }
   }
   ```

3. **Gọi đúng 1 dòng trong `Program.cs` của service con**:
   ```csharp
   var builder = WebApplication.CreateBuilder(args);

   // ĐĂNG KÝ 1 DÒNG NÀY (Dev nghiệp vụ không cần viết code tracing gì thêm):
   builder.Services.AddServiceObservability(builder.Configuration, "Catalog.Api");

   // Tiếp tục code DI, gRPC và DB bình thường...
   ```

### Bước 7: Nhận Thông Tin Định Danh Người Dùng từ Gateway (x-user-id & x-user-roles)
Gateway đã đảm nhiệm toàn bộ việc giải mã và xác thực JWT Bearer Token. Khi gọi gRPC xuống service con, Gateway tự động gắn các headers sau vào gRPC Metadata:
- `x-user-id`: Mã UUID định danh của người dùng (`sub` claim).
- `x-user-roles`: Danh sách vai trò (`admin,seller,buyer`...)
- `x-user-email`: Email người dùng.
- `x-trace-id`: Trace ID nhất quán.

**Service backend KHÔNG CẦN phân giải lại JWT token**, chỉ cần lấy trực tiếp:
```csharp
public override async Task<UserProfile> GetProfile(UserRequest request, ServerCallContext context)
{
    var userId = context.RequestHeaders.Get("x-user-id")?.Value ?? request.UserId;
    var roles = context.RequestHeaders.Get("x-user-roles")?.Value;
    // Thực thi nghiệp vụ ngay với userId đã được xác thực an toàn!
}
```


---

## 5. Quy Trình Test Khi Bạn Code Xong Một Service

1. Khởi động Service của bạn tại port tương ứng (ví dụ Wallet Service tại port `8008`).
2. Mở file `Nexus.Gateway/gateway.runtime.json` (hỗ trợ Hot-Reload tức thì, không cần khởi động lại Gateway):
   ```json
   "wallet": {
     "Mode": "Real",
     "Address": "http://localhost:8008"
   }
   ```
   *(Hoặc cấu hình mặc định trong `appsettings.Development.json`).*
3. Bật Gateway (nếu chưa chạy: `./run-dev.sh` hoặc `dotnet run --project Nexus.Gateway`).
4. Mở Swagger Gateway `http://localhost:8000/swagger`, gọi thử API `/api/v1/wallet/balance`:
   - Gateway sẽ tự động chuyển tiếp cuộc gọi gRPC sang backend thật của bạn tại port `8008`.
   - Các API khác (Catalog, Users, Commerce...) vẫn được Gateway phục vụ bằng Seed mock data mượt mà từ thư mục `Seeds/`!

---

Chúc bạn code backend thuận lợi và hiệu quả!
