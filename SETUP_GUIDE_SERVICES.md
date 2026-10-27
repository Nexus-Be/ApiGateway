# Hướng Dẫn Kiến Trúc & Thiết Lập Các Microservices (SETUP_GUIDE_SERVICES.md)
> **Trạng thái triển khai (2026-10-24):** ⚠️ Một phần — Gateway có 4/15 controller chứa action (16 action), **1/8** gRPC client (`users`), `shop.proto` chỉ có `UsersApi` 5 rpc, 11 controller là stub 0 action. Các bước trong tài liệu này là **hướng dẫn mục tiêu** cho service backend; phần nào chưa có trong code đều được ghi rõ ❌/⚠️.
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-24.
Tài liệu này cung cấp toàn bộ quy trình thiết lập, chuẩn giao tiếp và các bước cần thực hiện để sẵn sàng kết nối các backend microservices với **Nexus API Gateway**.
---
## 1. Bản Đồ Phân Bổ Cổng Mạng (Port Map) & Quy Hoạch 8 Services
Gateway làm đầu mối duy nhất cho Frontend gọi vào ở cổng `8080` (bất biến — `Dockerfile EXPOSE 8080`, `launchSettings.json`, `ASPNETCORE_URLS=http://+:8080`). Toàn bộ 8 Microservices chạy độc lập từ cổng `8001` đến `8008`.
Cột **Trạng thái phía Gateway** dưới đây là phần **đã kiểm chứng trong repo này**; cột Database là **quy hoạch** của monorepo, repo gateway không chứa DB nào (0 `DbContext`, 0 `DbSet`).
| STT | Tên Service | Port | Database *(quy hoạch)* | Nhiệm vụ chính | Trạng thái phía Gateway | Bằng chứng |
|:---:|:---|:---:|:---|:---|:---|:---|
| **0** | **Nexus.Gateway** | **8080** | *(Redis: 6379)* | Edge Proxy, JWT/RBAC, RateLimit, MockEngine, OpenTelemetry | ⚠️ Một phần | 4/15 controller có action (16 action), 1/8 gRPC client |
| **1** | **Users Service** | **8001** | `usermanagement_db` (5433) | Đăng ký, đăng nhập, phát hành JWT, User Profile, RBAC, Duyệt Seller | ✅ CÓ (client + proto + 5 action) | `Extensions/GrpcClientsExtension.cs`, `Contracts/shop.proto`, `AuthController`, `UsersController` |
| **2** | **Catalog Service** | **8002** | `category_db` (5434) | Danh mục, sản phẩm, tồn kho ảo | ❌ CHƯA CÓ client/rpc — controller `Products`/`Categories`/`Inventory` là stub 0 action | `Controllers/{Products,Categories,Inventory}Controller.cs` |
| **3** | **Commerce Service** | **8003** | `commerce_db` (5435) | Giỏ hàng, đơn hàng, Checkout Saga | ❌ CHƯA CÓ client/rpc — controller `Cart`/`Orders` stub | `Controllers/{Cart,Orders}Controller.cs` |
| **4** | **Log Service** | **8004** | `log_db` (5436) | Nhật ký tập trung, event consumer | ❌ CHƯA CÓ client/rpc — controller `Log` stub | `Controllers/LogController.cs` |
| **5** | **Auction Service** | **8005** | `auction_db` (5437) | Phiên đấu giá, bid | ❌ CHƯA CÓ client/rpc — controller `Auction` stub | `Controllers/AuctionController.cs` |
| **6** | **Fulfillment Service**| **8006** | `fulfillment_db` (5438) | Tồn kho vật lý, phí ship, vận đơn | ❌ CHƯA CÓ client/rpc — controller `Fulfillment` stub | `Controllers/FulfillmentController.cs` |
| **7** | **Notification Service**| **8007** | `notification_db` (5439)| Thông báo, cài đặt thông báo | ❌ CHƯA CÓ client/rpc — controller `Notification` stub | `Controllers/NotificationController.cs` |
| **8** | **Wallet Service** | **8008** | `wallet_db` (5440) | Ví điện tử, số dư, nạp/rút | ❌ CHƯA CÓ client/rpc — controller `Wallet` stub | `Controllers/WalletController.cs` |
> ⚠️ **Đính chính**: bản cũ ghi Gateway "✅ Hoàn thành 100%". Không đúng — 11/15 controller rỗng và 7/8 service chưa có client gRPC, nên **chỉ luồng `users` (auth + profile) chạy được end-to-end**.
---
## 2. Các Việc Đã Hoàn Thiện Tại Gateway
Chạy `dotnet test ApiGateway.sln -c Release` để lấy số liệu thật; tài liệu này **không** trích dẫn con số pass cố định (quy mô hiện tại: 62 attribute `[Fact]`/`[Theory]` trên 19 file). Tests Gateway không chứng minh các backend đã triển khai/tích hợp đầy đủ.
1. **REST sang gRPC Translation**: ⚠️ MỘT PHẦN — **1/8** client typed được đăng ký: `AddNamedGrpcClient<UsersApi.UsersApiClient>("users")` (`Extensions/GrpcClientsExtension.cs`). Bản cũ ghi "8 gRPC Clients typed" là **sai**.
2. **Mock Engine**: ✅ CÓ — hai cơ chế chạy song song: seed fixture (`Dispatching/MockDataMapper.cs`, 9 file trong `Seeds/`) và declarative mock (`MockEngine/MockEngineMiddleware.cs`, 6 file `Mocks/*.mock.json`).
3. **Cô lập trạng thái Real vs Mock theo từng Service**: ⚠️ MỘT PHẦN — đổi mode hoạt động (`Services/GatewayRuntimeManager.cs`, hot-reload `gateway.runtime.json`), nhưng chỉ `users` có client; bật `catalog: Real` **không** gọi được Catalog.
4. **Authentication & RBAC**: ✅ CÓ — kiểm tra JWT chặt chẽ, chặn mock token khi service `Real` (`Security/MockSecurityAdapterMiddleware.cs:45`, `Security/SecurityConfiguration.cs`). `AuthMode: Bypass` chỉ Development và không áp dụng cho control-plane. ⚠️ Không service nào **phát** JWT (`rpc Login` chưa implement ở User service).
5. **IETF Standard Errors**: ✅ CÓ — `Middleware/GatewayExceptionHandler.cs` map sang Problem Details RFC 9110 (`ApiErrorResponse`, `errorCode` SCREAMING_SNAKE_CASE).
6. **Observability**: ✅ CÓ — `OpenTelemetry.* 1.18.0` trong `Nexus.Gateway.csproj:20-23`; `AddGatewayTelemetry` bật AspNetCore/HttpClient/Grpc.Net.Client + `BackendDispatcher` source, exporter OTLP chỉ khi có `OTEL_EXPORTER_OTLP_ENDPOINT`. ⚠️ Không có instrumentation EF Core/Redis/MassTransit ở gateway (0 package tương ứng). Grafana Tempo/Jaeger/Prometheus **không** nằm trong `docker-compose.yml` của repo này.
7. **Health Prober Daemon & Circuit Breaker**: ⚠️ MỘT PHẦN — prober ✅ (`HealthProber/HealthProberManager.cs`, mặc định `Disabled`, Interval 60s / Timeout 30s / FailureThreshold 5 / StartPeriod 60s), circuit breaker ✅ (`Dispatching/GatewayDispatcher.cs:103-113,164-176` trả 503 `DOWNSTREAM_UNHEALTHY`).
   - ❌ **Saga Rollback chưa có**: `HealthProber/SagaRollbackPublisher.cs` chỉ ghi warning log, **không** publish RabbitMQ (gateway có 0 package MassTransit).
   - ⚠️ **Docker auto-recovery không chạy trong container**: `HealthProber/DockerContainerRecoveryService.cs` gọi CLI `docker restart`; compose có mount `/var/run/docker.sock` nhưng image `mcr.microsoft.com/dotnet/aspnet:8.0` **không cài docker CLI**.
---
## 3. Các Phần Còn Lại Cho Gateway (Nếu Muốn Mở Rộng Thêm Trong Tương Lai)
Cần hoàn thiện RPC, triển khai backend và test tích hợp cho từng service trước khi bật Real trong môi trường dùng chung:
- ❌ **11 controller stub** cần viết action (`AdminUsers`, `Auction`, `Cart`, `Categories`, `Fulfillment`, `Inventory`, `Log`, `Notification`, `Orders`, `Products`, `Wallet`).
- ❌ **7 gRPC client** còn thiếu + mở rộng `Contracts/shop.proto` ngoài `UsersApi`.
- ❌ **Output Caching**: cache Redis cho endpoint đọc công khai ít thay đổi (ví dụ `GET /api/v1/catalog/categories` — hiện chưa có action).
- ❌ **BFF (Backend for Frontend)**: ghép dữ liệu đa nguồn cho màn hình mobile.
---
## 4. Các Bước Thiết Lập Cho Từng Microservice Backend
Khi bắt tay vào code hoặc hoàn thiện từng service backend (`Users`, `Catalog`, `Commerce`, `Wallet`...), hãy làm theo các bước sau.
> ⚠️ **Lưu ý phạm vi**: repo gateway là **self-contained**; các bước dưới đây hướng dẫn sửa code ở **repo backend** tương ứng, không phải tạo file trong repo này.
### Bước 1: Hạ Tầng Database & RabbitMQ (Docker Compose)
⚠️ **`docker-compose.yml` của repo này chỉ định nghĩa service `gateway`** (port `${GATEWAY_PORT:-8080}:8080`, mount docker socket, network `nexus-network`) — **không** có Postgres/RabbitMQ/Tempo/Grafana.
Hạ tầng DB + RabbitMQ phải chạy từ stack của monorepo hoặc từ repo backend tương ứng:
```bash
# Từ thư mục gốc monorepo, dùng compose của hạ tầng (không phải compose của gateway)
docker compose -f <infra-compose>.yml up -d
```
Kiểm tra RabbitMQ Management UI tại `http://localhost:15672` (user/password **lấy từ biến môi trường**, xem Bước 4).
### Bước 2: Đồng Bộ Proto Contract
⚠️ **Không có "nguồn sự thật duy nhất"**: hiện tồn tại nhiều bản `shop.proto` trong monorepo với nội dung khác nhau (khác hash). `Nexus.Gateway/Contracts/shop.proto` chỉ khai báo `csharp_namespace = "ShoppingCart.Contracts.V1"`, `package shoppingcart.v1` và **duy nhất** `service UsersApi` (5 rpc: `Register`, `Login`, `VerifyOtp`, `ResendOtp`, `GetProfile`).
Quy tắc thực tế:
- Muốn thêm service mới ⇒ **thêm service vào `shop.proto`** (rpc mới) rồi đăng ký client ở `Extensions/GrpcClientsExtension.cs`.
- **Không tự đổi tag/field number** của message đang dùng (phá vỡ tương thích wire).
- Đồng bộ đúng một bản proto cho service backend; không giả định bản gateway là bản chuẩn cho tất cả.
Khai báo trong `.csproj` của service backend:
```xml
<Protobuf Include="Contracts/shop.proto" GrpcServices="Server" />
```
### Bước 3: Implement gRPC Service Server
> ⚠️ **Đặc tả mục tiêu, chưa triển khai.** `WalletApi`/`Wallet`/`UserRequest` **không tồn tại** trong `Contracts/shop.proto` hiện tại — ví dụ dưới đây là mẫu để triển khai *sau khi* bổ sung rpc (Bước 2), không phải API đang chạy.
Ví dụ minh họa cách kế thừa lớp base do gRPC sinh ra:
```csharp
// Đặc tả mục tiêu — class base chỉ tồn tại sau khi thêm service vào shop.proto
public class WalletGrpcService : WalletApi.WalletApiBase
{
    private readonly IWalletService _walletService;
    public WalletGrpcService(IWalletService walletService) => _walletService = walletService;
    public override async Task<WalletReply> GetWallet(WalletRequest request, ServerCallContext context)
    {
        var data = await _walletService.GetByUserIdAsync(request.UserId);
        return new WalletReply { Id = data.Id.ToString(), UserId = data.UserId, Balance = data.Balance.ToString("0.00") };
    }
}
```
Đăng ký trong `Program.cs` của service:
```csharp
builder.Services.AddGrpc();
app.MapGrpcService<WalletGrpcService>();
```
Mẫu **đang chạy thật** để tham chiếu: `UsersApi` — client phía gateway gọi `RegisterAsync`/`LoginAsync`/`VerifyOtpAsync`/`ResendOtpAsync`/`GetProfileAsync` (`AuthController.cs`, `UsersController.cs`).
### Bước 4: Thiết Lập RabbitMQ & MassTransit Event Bus
Phiên bản MassTransit thống nhất trong các `.csproj` backend hiện tại là **8.3.0** (không phải 8.4.1):
```csharp
builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", 5672, "/", h =>
        {
            h.Username(builder.Configuration["RabbitMq:User"] ?? "guest");
            // KHÔNG hardcode mật khẩu trong code/tài liệu: lấy từ secret/env
            h.Password(builder.Configuration["RabbitMq:Password"]
                ?? throw new InvalidOperationException("RabbitMq:Password is not configured."));
        });
        cfg.ConfigureEndpoints(context);
    });
});
```
> ⚠️ Mật khẩu RabbitMQ **không** được ghi trong tài liệu này. Bản cũ hardcode `MatkhaucuaTui123@` — nếu mật khẩu đó còn tồn tại ở bất kỳ repo nào phải coi là **đã lộ** và đổi ngay.
### Bước 5: Thiết Lập Saga Điều Phối Thanh Toán (Checkout & Wallet Saga)
> ❌ **Đặc tả mục tiêu, chưa triển khai.** Không có Saga State Machine nào trong code hiện tại; gateway chỉ có `SagaRollbackPublisher` **ghi log**.
Luồng mục tiêu:
```
1. Client gửi POST /api/v1/commerce/orders qua Gateway -> CommerceService tạo Order (Pending).
2. CommerceService bắn event OrderSubmitted lên RabbitMQ.
3. CatalogService giữ kho ảo (ReserveStock) -> StockReservedEvent.
4. WalletService trừ tiền (DeductBalance) -> PaymentCompletedEvent.
5. CommerceService cập nhật Order (Confirmed/Paid).
6. [BÙ TRỪ] Wallet trừ tiền thất bại -> PaymentFailedEvent -> Catalog nhả kho (ReleaseStock), Commerce hủy đơn.
```
### Bước 6: Thiết Lập Distributed Tracing (OpenTelemetry) — Zero-Touch cho Dev
> ⚠️ Đây là **quy ước mục tiêu** cho service backend; gateway dùng implementation riêng làm mẫu tham chiếu: `Extensions/TelemetryExtension.cs` → `AddGatewayTelemetry(IConfiguration)` (gọi tại `Program.cs:17`), đăng ký AspNetCore + HttpClient + `Grpc.Net.Client` + source nghiệp vụ, exporter OTLP chỉ bật khi có endpoint.
1. **Thêm package vào `.csproj` service con**:
   ```xml
   <ItemGroup>
     <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.18.0" />
     <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.18.0" />
     <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.18.0" />
     <PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.18.0" />
     <PackageReference Include="OpenTelemetry.Instrumentation.EntityFrameworkCore" Version="1.0.0-beta.11" /> <!-- nếu dùng EF Core -->
   </ItemGroup>
   ```
2. **Tạo extension dùng chung trong repo của service đó** (tên hàm hiện chưa đồng nhất giữa các repo — thống nhất trước khi copy):
   ```csharp
   public static IServiceCollection AddServiceObservability(
       this IServiceCollection services, IConfiguration configuration, string serviceName)
   {
       var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? configuration["Otel:Endpoint"];
       services.AddOpenTelemetry()
           .ConfigureResource(r => r.AddService(serviceName))
           .WithTracing(t =>
           {
               t.AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSource("Grpc.Net.Client")
                .AddSource("MassTransit");
               if (!string.IsNullOrEmpty(otlpEndpoint))
                   t.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
           });
       return services;
   }
   ```
3. **1 dòng trong `Program.cs`** của service con:
   ```csharp
   builder.Services.AddServiceObservability(builder.Configuration, "Catalog.Api");
   ```
> ⚠️ Không có endpoint OTLP ⇒ exporter **không** được bật (không có span nào rời khỏi process). Tempo/Grafana cũng không nằm trong compose của repo này.
### Bước 7: Nhận Thông Tin Định Danh Người Dùng từ Gateway (x-user-id & x-user-roles)
Gateway đảm nhiệm giải mã và xác thực JWT Bearer Token. Khi gọi gRPC xuống service con, `Dispatching/GrpcServiceCaller.cs:62-89` gắn các metadata sau:
- `x-user-id`: UUID người dùng (`sub` claim) — `:76`
- `x-user-roles`: danh sách vai trò, phân tách bởi dấu phẩy — `:82`
- `x-user-email`: email người dùng — `:89`
- `x-trace-id`: Trace ID nhất quán — `:68`
- `Idempotency-Key`: chuyển tiếp nguyên văn nếu client gửi — `:62` (⚠️ chưa validate định dạng UUID)
> ⚠️ Hiện các metadata này chỉ thực sự được gửi cho client `users` (client duy nhất đã đăng ký).
**Service backend KHÔNG CẦN phân giải lại JWT token**, chỉ cần đọc trực tiếp:
```csharp
public override async Task<UserProfile> GetProfile(UserProfileRequest request, ServerCallContext context)
{
    var userId = context.RequestHeaders.Get("x-user-id")?.Value ?? request.UserId;
    var roles = context.RequestHeaders.Get("x-user-roles")?.Value;
    // Xác thực đã xong ở Gateway — nhưng ĐỪNG tin header nếu service có thể bị gọi trực tiếp.
}
```
> ⚠️ Tin tuyệt đối header chỉ an toàn khi service **không** expose port ra ngoài. 8 backend service hiện **không** có authentication riêng, nên phải chặn truy cập trực tiếp ở network layer.
---
## 5. Quy Trình Test Khi Bạn Code Xong Một Service
1. Khởi động service của bạn ở port tương ứng (ví dụ `8002` cho Catalog).
2. Mở `Nexus.Gateway/gateway.runtime.json` (nạp với `reloadOnChange: true` ⇒ hot-reload, không cần restart):
   ```json
   "catalog": {
     "Mode": "Real",
     "Address": "http://localhost:8002"
   }
   ```
3. ⚠️ **Chỉ service `users` chạy được `Real`** hiện tại. Với service khác phải làm Bước 2 (thêm rpc + client) và viết action trong controller tương ứng (hiện là stub 0 action), nếu không sẽ nhận lỗi vì không có client/không có action.
4. Bật Gateway (nếu chưa chạy: `./run-dev.sh` hoặc `dotnet run --project Nexus.Gateway`) ⇒ `http://localhost:8080`.
5. Endpoint **đang chạy thật** để test nhanh:
   - `POST http://localhost:8080/api/v1/auth/register` (Mock: `Seeds/auth/register.json`)
   - `POST http://localhost:8080/api/v1/auth/login` (Mock: `Seeds/auth/login.json`; Real: chưa chạy vì `rpc Login` chưa implement)
   - `GET http://localhost:8080/api/v1/users/me` (cần token hợp lệ)
   - Quản trị: `GET /api/v1/gateway/modes`, `PUT /api/v1/gateway/mode`, `GET /api/v1/gateway/prober/status`
6. ⚠️ Các API khác **không** "được phục vụ bằng Seed mock data" như bản cũ mô tả: chỉ `auth` (4 file) và `users` (5 file) có seed; route của 11 controller stub trả **404** trừ khi có `Mocks/*.mock.json` khớp (6 file: `auth-login`, `auth-register`, `auth-resend-otp`, `auth-verify-otp`, `catalog-products`, `users-me`).
7. Swagger: `http://localhost:8080/swagger` chỉ hiển thị **16 action** của 4 controller có action.
---
Chúc bạn code backend thuận lợi và hiệu quả!