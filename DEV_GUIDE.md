# CẨM NANG DÀNH CHO DEVELOPER (DEV_GUIDE.md)
## API GATEWAY — DỄ ĐỌC • DỄ THAO TÁC • DỄ TRIỂN KHAI • DỄ UPDATE

Tài liệu này là sổ tay thực chiến nhanh dành cho mọi thành viên trong team khi cần thao tác, phát triển thêm API mới hoặc cập nhật dữ liệu Mock trên **ApiGateway**.

---

## 1. Bản Đồ Cấu Trúc Dự Án (Dễ Đọc & Dễ Tìm)

Toàn bộ mã nguồn được bố trí theo kiến trúc 3 khối độc lập (3-Tier Decoupling: Input → Middle → Output):

```
Nexus.Gateway/
├── Program.cs                # File khởi động siêu gọn (< 35 dòng), đọc hiểu toàn bộ luồng trong 10 giây
│
├── Controllers/              # ĐẦU VÀO: 10 Controllers tiêu chuẩn [ApiController] ControllerBase
│   ├── AuthController.cs     # /gateway/api/v1/auth (login, register)
│   ├── UsersController.cs    # /gateway/api/v1/users, /gateway/api/v1/admin/users
│   ├── CatalogController.cs  # /gateway/api/v1/catalog, /gateway/api/v1/seller/products, inventory
│   ├── CommerceController.cs # /gateway/api/v1/commerce/cart, orders, seller/admin orders
│   ├── AuctionController.cs  # /gateway/api/v1/auctions, bids
│   ├── FulfillmentController.cs # /gateway/api/v1/fulfillment (shipping, inventory, tracking)
│   ├── NotificationController.cs# /gateway/api/v1/notifications
│   ├── AuditController.cs    # /gateway/api/v1/audit (logs, event types)
│   ├── WalletController.cs   # /gateway/api/v1/wallet (balance, deposit, withdraw, transfer)
│   └── GatewayControlController.cs # /gateway/api/v1/gateway, /mock-engine (control plane)
│
├── Dtos/                     # DTOs tách biệt Requests (*Requests.cs) và Responses (*Responses.cs)
│   ├── Auth/                 # AuthRequests.cs, AuthResponses.cs
│   ├── Users/                # UserRequests.cs, UserResponses.cs
│   ├── Catalog/              # CatalogRequests.cs, CatalogResponses.cs
│   ├── Commerce/             # CommerceRequests.cs, CommerceResponses.cs
│   ├── Auction/              # AuctionRequests.cs, AuctionResponses.cs
│   ├── Fulfillment/          # FulfillmentRequests.cs, FulfillmentResponses.cs
│   ├── Notification/         # NotificationRequests.cs, NotificationResponses.cs
│   ├── Audit/                # AuditRequests.cs, AuditResponses.cs
│   ├── Wallet/               # WalletRequests.cs, WalletResponses.cs
│   └── GatewayControl/       # GatewayControlRequests.cs, GatewayControlResponses.cs
│
├── Dispatching/              # PHẦN GIỮA: Điều phối độc lập (ModeEvaluator, MockDataMapper, GrpcServiceCaller, GatewayDispatcher)
│   ├── IModeEvaluator.cs     # Đánh giá mode Mock/Real, runtime hot-reload & header override
│   ├── IMockDataMapper.cs    # Đọc Seeds JSON, thẩm định contract qua Protobuf Parser
│   ├── IGrpcServiceCaller.cs # Forward metadata (token, idempotency, traceId) & gọi gRPC backend
│   └── IGatewayDispatcher.cs # Bộ điều phối chính kết hợp 3 khối trên
│
├── Common/                   # Tiện ích dùng chung (ValidationHelpers: Bad, PageOk, GuidOk)
│   └── ValidationHelpers.cs
│
├── Responses/                # ĐẦU RA: Unified Output Factory (ApiResponseFactory, ApiResponse, ApiErrorResponse)
│   ├── ApiResponse.cs        # Envelope thành công
│   ├── ApiErrorResponse.cs   # Envelope lỗi RFC 9110
│   ├── PagedData.cs          # Envelope phân trang
│   ├── ApiResponseFactory.cs # Chuẩn hóa toàn bộ response & resolve traceId
│   └── HttpContextUserExtensions.cs # Extension đọc User/Role từ HttpContext
│
├── Extensions/               # CẤU HÌNH HỆ THỐNG (Service Registration & Pipeline)
│   ├── ConfigurationExtensions.cs  # Nạp cấu hình & bind GatewayOptions
│   ├── CoreServicesExtension.cs    # Đăng ký Controller, Dispatching, Redis, ExceptionHandler
│   ├── GrpcClientsExtension.cs     # Đăng ký 8 typed gRPC Clients
│   ├── PipelineExtensions.cs       # Chuỗi Middleware xử lý request
│   ├── SwaggerExtension.cs         # Cấu hình Swagger UI & Mock filter
│   └── TelemetryExtension.cs       # Cấu hình OpenTelemetry Tracing & Metrics
│
├── Contracts/
│   └── shop.proto            # Nguồn sự thật duy nhất (Proto Contract) tự động sinh C# code
│
└── Seeds/                    # DỮ LIỆU SEED JSON THEO TỪNG SERVICE & ENTITY
    ├── users/                # login.json, profile.json, users.json...
    ├── catalog/              # products.json, product.json, categories.json...
    ├── commerce/             # cart.json, orders.json, order.json...
    └── ...                   # auction/, fulfillment/, notification/, wallet/
```

---

## 2. Quy Trình 3 Bước Thêm Mới 1 API (Dễ Thao Tác & Update)

Giả sử bạn muốn thêm API: `GET /gateway/api/v1/catalog/brands` (Lấy danh sách thương hiệu).

### Bước 1: Khai báo vào `Contracts/shop.proto`
Mở file [shop.proto](Nexus.Gateway/Contracts/shop.proto), bổ sung RPC và Message:
```protobuf
service CatalogApi {
  // Thêm method mới
  rpc ListBrands(PageRequest) returns (BrandPage);
}

message BrandPage {
  repeated Brand items = 1;
  int32 total = 2;
}

message Brand {
  string id = 1;
  string name = 2;
  string logo_url = 3;
}
```
*Lưu ý: Ngay sau khi lưu file, trình biên dịch `protoc` sẽ tự động sinh mã C# cho `BrandPage`, `Brand`, `CatalogApiClient`.*

---

### Bước 2: Tạo File Seed Mẫu `Seeds/catalog/brands.json`
Tạo file `Nexus.Gateway/Seeds/catalog/brands.json`:
```json
{
  "items": [
    { "id": "brand-01", "name": "Keychron", "logoUrl": "https://example.com/keychron.png" },
    { "id": "brand-02", "name": "Akko", "logoUrl": "https://example.com/akko.png" }
  ],
  "total": 2
}
```
*💡 Lưu ý an toàn: Khi chạy Mock, Gateway tự động ép file JSON này qua `BrandPage.Parser.ParseJson()`. Nếu bạn viết sai tên trường hoặc sai kiểu dữ liệu, hệ thống sẽ báo lỗi ngay, giúp bạn không bao giờ làm sai contract với Frontend!*

---

### Bước 3: Khai báo Action trong Controller tương ứng (`Controllers/CatalogController.cs`)
Mở file `Controllers/CatalogController.cs`, thêm Action tiêu chuẩn (gọi điều phối đúng 1 dòng):
```csharp
[HttpGet("gateway/api/v1/catalog/brands")]
[AllowAnonymous]
[ProducesResponseType(typeof(ApiResponse<PagedData<Brand>>), StatusCodes.Status200OK)]
public Task<IResult> ListBrands([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
{
    if (!ValidationHelpers.PageOk(page, pageSize)) 
        return Task.FromResult(ValidationHelpers.Bad(HttpContext, ValidationHelpers.PageMessage, field: "Pagination"));

    return _dispatcher.DispatchPagedAsync(
        HttpContext, 
        "catalog", 
        "brands", 
        page, 
        pageSize, 
        o => _client.ListBrandsAsync(new PageRequest { Page = page, PageSize = pageSize }, o), 
        BrandPage.Parser
    );
}
``` 
        o => client.ListBrandsAsync(new PageRequest { Page = page, PageSize = pageSize }, o), 
        BrandPage.Parser);
}).Produces<ApiResponse<PagedData<Brand>>>().AllowAnonymous();
```

👉 **Xong!** 
- Khi service `catalog` ở mode `Mock`: Tự động trả dữ liệu từ file `brands.json`.
- Khi service `catalog` ở mode `Real`: Tự động gọi gRPC sang `CatalogService:8002`.
- Tự động xuất hiện trên **Swagger UI** kèm tài liệu mô tả đầy đủ.

---

## 3. Cách Triển Khai & Khởi Chạy (Dễ Triển Khai)

### Cách 1: Khởi Chạy Trực Tiếp Bằng 1 Lệnh (Local Dev)
```bash
# Trên Linux / macOS:
./run-dev.sh

# Trên Windows (PowerShell):
./run-dev.ps1
```
Gateway sẽ khởi chạy tại: `http://localhost:8000/` (tự động điều hướng sang Swagger UI).

---

### Cách 2: Khởi Chạy Qua Docker Compose (Kèm Redis)
Chỉ cần 1 lệnh duy nhất:
```bash
docker compose up -d
```
Hệ thống sẽ tự động khởi động:
- **Redis Server**: `localhost:6379` (Phục vụ Rate Limiting phân tán).
- **ApiGateway**: `localhost:8000` (Đã cấu hình sẵn sàng kết nối Redis).

Để dừng:
```bash
docker compose down
```

---

## 4. Chuyển Đổi Mock $\leftrightarrow$ Real Cho Từng Service Khi Backend Code Xong

Gateway cho phép bạn chuyển đổi từng service riêng biệt mà không ảnh hưởng tới các service khác.

Mở file [gateway.runtime.json](Nexus.Gateway/gateway.runtime.json):
```json
"Services": {
  "catalog": {
    "Mode": "Real",               // Đổi từ "Mock" sang "Real"
    "Address": "http://localhost:8002"  // Địa chỉ service backend Catalog đang chạy
  },
  "commerce": {
    "Mode": "Mock"                // Các service khác vẫn giữ Mock bình thường
  }
}
```
Lưu file $\rightarrow$ **Có hiệu lực tức thì (Hot-Reload), KHÔNG cần khởi động lại Gateway!**

---

## 5. Chế Độ Bypass (Frontend Dev Test Không Cần Đăng Nhập)

Nếu bạn là Frontend Developer và muốn test nhanh các màn hình giao diện mà không muốn mất thời gian đăng nhập:
1. Mở file [gateway.runtime.json](Nexus.Gateway/gateway.runtime.json).
2. Đổi `"AuthMode": "Standard"` $\rightarrow$ `"AuthMode": "Bypass"`.
3. Lúc này, bạn có thể gọi mọi API mock (kể cả `/users/me`, giỏ hàng, trang admin) mà **không cần truyền bất kỳ token nào**, Gateway sẽ tự động cấp danh tính giả lập tương ứng.
4. Khi muốn kiểm tra phân quyền nghiêm ngặt lại: Đổi lại `"AuthMode": "Standard"`.

---

## 6. Lệnh Kiểm Thử Toàn Bộ Hệ Thống (Automated Tests)

Mỗi khi thêm tính năng mới hoặc refactor, hãy chạy bộ kiểm thử tự động để bảo đảm an toàn 100%:
```bash
dotnet test ApiGateway.sln -c Release
```
Bộ test suite bao gồm 46 bài test kiểm tra tự động toàn bộ bề mặt API, bảo mật JWT/RBAC, Rate Limit và trạng thái chuyển đổi Real/Mock.
