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
| **4** | **Audit Service** | **8004** | `audit_db` (5435) | Lưu vết audit logs kiểm toán qua RabbitMQ event consumer | Sẵn sàng kết nối |
| **5** | **Auction Service** | **8005** | `auction_db` (5436) | Phiên đấu giá, trả giá (bid), đồng hồ đếm ngược | Sẵn sàng kết nối |
| **6** | **Fulfillment Service**| **8006** | `fulfillment_db` (5437) | Tồn kho vật lý kho bãi, tính phí vận chuyển, tra cứu mã vận đơn | Sẵn sàng kết nối |
| **7** | **Notification Service**| **8007** | `notification_db` (5438)| Gửi thông báo, lịch sử thông báo, cài đặt thông báo người dùng | Sẵn sàng kết nối |
| **8** | **Wallet Service** | **8008** | `wallet_db` (5439) | Ví điện tử riêng biệt, số dư, nạp/rút tiền, chuyển tiền, thanh toán đơn hàng | Sẵn sàng kết nối |

---

## 2. Các Việc Đã Hoàn Thiện Tại Gateway

Gateway hiện tại đã đầy đủ mọi nền tảng và đã pass 46/46 unit & integration tests:
1. **REST sang gRPC Translation**: Khai báo 8 gRPC Clients typed an toàn theo contract `shop.proto`.
2. **Mock Engine**: Hỗ trợ chạy Mock độc lập từng service mà không cần backend chạy thật.
3. **Cô lập trạng thái Real vs Mock theo từng Service**: Bạn có thể bật 1 service bất kỳ sang `Real` (ví dụ `catalog: Real`) để code và test, trong khi 7 service còn lại vẫn chạy `Mock` bình thường.
4. **Authentication & RBAC**: Kiểm tra JWT Token chặt chẽ (chặn mock token khi service ở chế độ Real). Tự động bypass role linh hoạt khi bật `AuthMode: Bypass` để test giao diện.
5. **IETF Standard Errors**: Trả mã lỗi Problem Details theo chuẩn quốc tế RFC 9110 & RFC 6585.
6. **Observability**: Tích hợp sẵn OpenTelemetry 1.18.0 Tracing và Metrics (hỗ trợ đẩy OTLP ra Jaeger/Prometheus).

---

## 3. Các Phần Còn Lại Cho Gateway (Nếu Muốn Mở Rộng Thêm Trong Tương Lai)

Gateway hiện tại đã hoàn thiện đầy đủ cho việc kết nối backend và test mock. Nếu dự án mở rộng quy mô lớn hơn, có thể bổ sung:
- **Output Caching**: Thêm cache Redis cho các endpoint đọc công khai ít thay đổi (ví dụ: `GET /gateway/api/v1/catalog/categories`).
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
1. Client gửi POST /gateway/api/v1/commerce/orders qua Gateway -> CommerceService tạo Order (Status = Pending).
2. CommerceService bắn event OrderSubmitted lên RabbitMQ.
3. CatalogService lắng nghe -> Giữ kho ảo (ReserveStock). Bắn StockReservedEvent.
4. WalletService lắng nghe -> Trừ tiền trong ví khách hàng (DeductBalance). Bắn PaymentCompletedEvent.
5. CommerceService nhận PaymentCompletedEvent -> Cập nhật Order (Status = Confirmed / Paid).
6. [BÙ TRỪ NẾU LỖI]: Nếu WalletService trừ tiền thất bại (không đủ tiền) -> Bắn PaymentFailedEvent -> CatalogService nhả kho (ReleaseStock) và CommerceService hủy đơn (OrderCancelled).
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
4. Mở Swagger Gateway `http://localhost:8000/swagger`, gọi thử API `/gateway/api/v1/wallet/balance`:
   - Gateway sẽ tự động chuyển tiếp cuộc gọi gRPC sang backend thật của bạn tại port `8008`.
   - Các API khác (Catalog, Users, Commerce...) vẫn được Gateway phục vụ bằng Seed mock data mượt mà từ thư mục `Seeds/`!

---

Chúc bạn code backend thuận lợi và hiệu quả!
