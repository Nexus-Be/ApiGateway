# ĐẶC TẢ KỸ THUẬT & KIẾN TRÚC CHI TIẾT API GATEWAY (.NET 8)

Tài liệu này cung cấp toàn bộ đặc tả kỹ thuật, kiến trúc module hóa mới, quy trình phát triển và hướng dẫn thực chiến cho lập trình viên (Developer Guide) khi làm việc với **ApiGateway** thuộc hệ sinh thái Microservices **Nexus**.

---

## 1. TỔNG QUAN KIẾN TRÚC VÀ VAI TRÒ CỦA API GATEWAY

**ApiGateway** đóng vai trò là **Edge Reverse Proxy** (Cổng vào duy nhất) cho toàn bộ hệ thống backend Nexus Microservices:
- **Giao tiếp ngoại vi (Client $\rightarrow$ Gateway)**: Sử dụng **RESTful JSON** (HTTP/1.1 và HTTP/2) với tiền tố chuẩn hóa duy nhất:
  $$\mathbf{/gateway/api/v1/\{service\}/...}$$
- **Giao tiếp nội bộ (Gateway $\rightarrow$ Microservices)**: Sử dụng **gRPC trên nền Protobuf v3** với hiệu năng cao, kiểu dữ liệu an toàn (strongly-typed), nhị phân nén và độ trễ thấp.
- **Độc lập phát triển (Mock Engine tích hợp)**: Cho phép Frontend làm việc và kiểm thử toàn bộ 8 services mà không cần bật bất kỳ backend service hay database nào. Từng service backend có thể chuyển đổi linh hoạt giữa chế độ `Mock` và `Real`.

### Bản Đồ Kết Nối Hệ Thống (Network & Port Map)

```
[ Frontend: React / Next.js / Mobile App ]
                   │
                   ▼ (HTTP/REST: /gateway/api/v1/... - Port 8000)
       ┌────────────────────────┐
       │       ApiGateway       │ ──(Cache / RateLimit)──▶ [ Redis : 6379 ]
       └────────────────────────┘
                   │ (gRPC Channel - Protobuf v3)
 ┌───────────────┬─┴─────────────┬───────────────┬───────────────┐
 ▼               ▼               ▼               ▼               ▼
UsersService  CatalogService  CommerceService  AuditService   AuctionService  ... (+ Fulfillment, Notification, Wallet)
(Port 8001)   (Port 8002)     (Port 8003)      (Port 8004)    (Port 8005)         (Ports 8006, 8007, 8008)
```

---

## 2. BẢN ĐỒ CẤU TRÚC THƯ MỤC & PHÂN BỔ TRÁCH NHIỆM

Hệ thống được thiết kế theo kiến trúc 3 khối độc lập (3-Tier Decoupling: Input → Middle → Output) với các Controller tiêu chuẩn `[ApiController] ControllerBase`, DTOs tách biệt Requests & Responses, và lõi điều phối `GatewayDispatcher`:

```
src/Services/ApiGateway/
├── ApiGateway.sln                        # Visual Studio / Rider Solution file
├── ADR.md                                # Ghi chép quyết định kiến trúc (Architecture Decision Records)
├── CONFIG_REFERENCE.md                   # Cẩm nang tra cứu cấu hình & biến môi trường
├── PLAN.md                               # Kế hoạch phát triển và tiến trình kiểm thử
├── README.md                             # Hướng dẫn nhanh cài đặt và khởi chạy
├── SETUP_GUIDE_SERVICES.md               # Hướng dẫn kết nối 8 microservices backend
├── dac_ta.md                             # Bản đặc tả kỹ thuật & tài liệu hướng dẫn này
├── input.md                              # Quy chuẩn dữ liệu đầu vào & format response
├── global.json                           # Cố định phiên bản .NET SDK (8.0.x)
│
├── Nexus.Gateway/                        # PROJECT CHÍNH (ASP.NET Core 8 Web API / Gateway)
│   ├── Program.cs                        # Khởi động ứng dụng tinh gọn (< 35 dòng)
│   ├── BackendDispatcher.cs              # Cầu nối tương thích ủy quyền sang IGatewayDispatcher
│   ├── GatewayOptions.cs                 # Lớp cấu hình strongly-typed (CORS, RateLimit, Services...)
│   ├── Dockerfile                        # File đóng gói Docker Image cho production
│   ├── appsettings.json                  # Cấu hình gốc (Base/Production)
│   ├── appsettings.Development.json      # Cấu hình môi trường Development (mặc định Mock bật)
│   ├── gateway.runtime.json              # Cấu hình runtime động (hot-reload mode Real/Mock)
│   │
│   ├── Contracts/
│   │   └── shop.proto                    # Nguồn sự thật duy nhất (Single Source of Truth) định nghĩa gRPC proto
│   │
│   ├── Common/                           # TẦNG DÙNG CHUNG (VALIDATION & HELPERS)
│   │   └── ValidationHelpers.cs          # Validation helpers: Bad(), PageOk(), GuidOk(), Roles, Consts
│   │
│   ├── Controllers/                      # ĐẦU VÀO: 10 CONTROLLERS TIÊU CHUẨN [ApiController] ControllerBase
│   │   ├── AuthController.cs             # Routes: /gateway/api/v1/auth/... (login, register)
│   │   ├── UsersController.cs            # Routes: /gateway/api/v1/users/..., /gateway/api/v1/admin/users, roles
│   │   ├── CatalogController.cs          # Routes: /gateway/api/v1/catalog/..., /gateway/api/v1/seller/products, inventory
│   │   ├── CommerceController.cs         # Routes: /gateway/api/v1/commerce/cart, orders, seller/admin orders
│   │   ├── AuctionController.cs          # Routes: /gateway/api/v1/auctions/... (auctions, bids)
│   │   ├── FulfillmentController.cs      # Routes: /gateway/api/v1/fulfillment/... (shipping, inventory, tracking)
│   │   ├── NotificationController.cs     # Routes: /gateway/api/v1/notifications/... (list, read, preferences)
│   │   ├── AuditController.cs            # Routes: /gateway/api/v1/audit/... (logs, event types)
│   │   ├── WalletController.cs           # Routes: /gateway/api/v1/wallet/... (balance, deposit, withdraw, transfer)
│   │   └── GatewayControlController.cs   # Routes: /gateway/api/v1/gateway/..., /mock-engine/... (control plane)
│   │
│   ├── Dtos/                             # CÁC DTO ĐƯỢC TÁCH BIỆT RÀNH MẠCH THEO SERVICE
│   │   ├── Auth/                         # AuthRequests.cs, AuthResponses.cs
│   │   ├── Users/                        # UserRequests.cs, UserResponses.cs
│   │   ├── Catalog/                      # CatalogRequests.cs, CatalogResponses.cs
│   │   ├── Commerce/                     # CommerceRequests.cs, CommerceResponses.cs
│   │   ├── Auction/                      # AuctionRequests.cs, AuctionResponses.cs
│   │   ├── Fulfillment/                  # FulfillmentRequests.cs, FulfillmentResponses.cs
│   │   ├── Notification/                 # NotificationRequests.cs, NotificationResponses.cs
│   │   ├── Audit/                        # AuditRequests.cs, AuditResponses.cs
│   │   ├── Wallet/                       # WalletRequests.cs, WalletResponses.cs
│   │   └── GatewayControl/               # GatewayControlRequests.cs, GatewayControlResponses.cs
│   │
│   ├── Dispatching/                      # PHẦN GIỮA: THE DISPATCHING CORE (ĐIỀU PHỐI KHÔNG IF/ELSE Ở CONTROLLER)
│   │   ├── IModeEvaluator.cs / ModeEvaluator.cs       # Đánh giá mode Mock/Real, hot-reload & header override
│   │   ├── IMockDataMapper.cs / MockDataMapper.cs     # Đọc Seed JSON, kiểm định hợp đồng qua Protobuf Parser
│   │   ├── IGrpcServiceCaller.cs / GrpcServiceCaller.cs # Forward metadata (JWT, Idempotency-Key, TraceId) & gọi gRPC
│   │   └── IGatewayDispatcher.cs / GatewayDispatcher.cs # Bộ điều phối chính kết hợp 3 khối trên
│   │
│   ├── Extensions/                       # MỞ RỘNG DI & PIPELINE TẬP TRUNG
│   │   ├── ConfigurationExtensions.cs    # Cấu hình Options & logging
│   │   ├── CoreServicesExtension.cs      # Đăng ký Controller, Dispatching core, Redis, ExceptionHandler
│   │   ├── GrpcClientsExtension.cs       # Đăng ký 8 typed gRPC clients
│   │   ├── PipelineExtensions.cs         # Cấu hình Middleware pipeline chuẩn REST
│   │   ├── SwaggerExtension.cs           # Swagger UI + Bearer Auth + MockEngineDocumentFilter
│   │   └── TelemetryExtension.cs         # Cấu hình OpenTelemetry tracing & metrics
│   │
│   ├── Middleware/
│   │   ├── GatewayExceptionHandler.cs    # Xử lý ngoại lệ tập trung, map gRPC RpcException sang HTTP RFC 9110
│   │   └── RedisRateLimitMiddleware.cs   # Giới hạn tần suất gọi phân tán bằng Redis (Fixed Window)
│   │
│   ├── MockEngine/
│   │   ├── IMockEngine.cs                # Interface quản lý nạp, khớp và hot-reload mock definitions
│   │   ├── MockEngine.cs                 # Triển khai MockEngine đọc file *.mock.json, FileSystemWatcher debounce
│   │   ├── MockEndpointDefinition.cs     # Schema POCO của một mock endpoint khai báo động
│   │   ├── MockRouteMatcher.cs           # Thuật toán so khớp URL động (hỗ trợ tham số URL {id}, regex)
│   │   ├── MockRouteMatch.cs             # Kết quả sau khi so khớp route
│   │   ├── MockEngineMiddleware.cs       # Middleware chặn request trước và phục vụ dữ liệu mock
│   │   └── MockEngineDocumentFilter.cs   # Tự động đẩy các mock endpoint động lên Swagger UI
│   │
│   ├── Seeds/                            # DỮ LIỆU SEED CHUẨN HÓA THEO SERVICE & ENTITY
│   │   ├── users/                        # Fixtures JSON cho UserService (login, register, profile, roles...)
│   │   ├── catalog/                      # Fixtures JSON cho CatalogService (products, categories, inventory...)
│   │   ├── commerce/                     # Fixtures JSON cho CommerceService (cart, orders, checkout...)
│   │   ├── audit/                        # Fixtures JSON cho AuditService (audit logs, event types...)
│   │   ├── auction/                      # Fixtures JSON cho AuctionService (auctions, bids...)
│   │   ├── fulfillment/                  # Fixtures JSON cho FulfillmentService (inventory, shipping quotes...)
│   │   ├── notification/                 # Fixtures JSON cho NotificationService (notifications, preferences...)
│   │   ├── wallet/                       # Fixtures JSON cho WalletService (balance, deposit, withdraw, transfer...)
│   │   └── declarative/                  # Các file *.mock.json khai báo endpoint độc lập
│   │
│   ├── Responses/                        # ĐẦU RA: UNIFIED OUTPUT FACTORY
│   │   ├── ApiResponse.cs                # Envelope định dạng phản hồi thành công chuẩn REST
│   │   ├── ApiErrorResponse.cs           # Envelope định dạng lỗi theo chuẩn IETF RFC 9110 / RFC 6585
│   │   ├── PagedData.cs                  # Envelope dữ liệu phân trang
│   │   ├── PaginationMetadata.cs         # Metadata phân trang (pageIndex, pageSize, totalPages, hasNext...)
│   │   ├── IApiResponseFactory.cs        # Interface factory tạo response
│   │   ├── ApiResponseFactory.cs         # Triển khai factory chuẩn hóa response & tự động resolve TraceId
│   │   └── HttpContextUserExtensions.cs  # Tiện ích đọc ClaimsPrincipal (sub, role) chuyển sang UserRequest
│   │
│   ├── Security/
│   │   ├── GatewayAuthorization.cs       # Định nghĩa các Policy phân quyền RBAC & đánh dấu Bypass
│   │   ├── GatewaySecurityHelper.cs      # Helper kiểm tra trạng thái Mock/Real của request, resolve policy
│   │   ├── SecurityConfiguration.cs      # Cấu hình xác thực JWT Bearer, token validation, Mock token handler
│   │   ├── MockSecurityAdapterMiddleware.cs # Middleware cấp danh tính giả lập (ClaimsPrincipal) khi test mock
│   │   └── EdgeMiddlewareConfiguration.cs   # Đăng ký CORS và RateLimiter built-in của ASP.NET Core
│   │
│   └── Services/
│       ├── IGatewayRuntimeManager.cs     # Interface quản lý runtime mode (Mock/Real, AuthMode)
│       └── GatewayRuntimeManager.cs      # Quản lý trạng thái Real/Mock theo service, ghi file gateway.runtime.json
│
└── Nexus.Gateway.Tests/                  # PROJECT KIỂM THỬ TỰ ĐỘNG (46/46 PASS 100%)
    ├── Factory.cs                        # Custom WebApplicationFactory dựng TestServer in-memory
    ├── ApiResponseFactoryTests.cs        # Unit tests kiểm tra định dạng envelope response & error
    ├── GatewayTests.cs                   # Integration tests kiểm tra Kestrel limits, timeouts, Redis failover
    ├── GatewaySurfaceTests.cs            # Tests kiểm tra toàn bộ bề mặt API các service
    ├── MockEngineTests.cs                # Tests kiểm tra thuật toán matcher, route precedence, dynamic mocks
    └── SevenServicesGatewayTests.cs      # Tests kiểm tra chi tiết cả 8 microservices ở 2 chế độ Standard & Bypass
```

---

## 3. QUY TRÌNH THÊM 1 API MỚI CHO DEVELOPER (CHỈ VỚI 4 BƯỚC ĐƠN GIẢN)

Nhờ kiến trúc module hóa và bộ điều phối trung gian (`BackendDispatcher`), quy trình thêm mới 1 API cực kỳ ngắn gọn, trực quan và không gây rối mã nguồn:

### Bước 1: Khai báo Contract trong Protobuf (`Contracts/shop.proto`)
Khai báo RPC method và Request/Response message tương ứng trong service:
```protobuf
service CatalogApi {
  // Thêm mới RPC method
  rpc GetBrandList(PageRequest) returns (BrandPage);
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

### Bước 2: Tạo File Seed Mẫu (`Seeds/{service}/{operation}.json`)
Tạo file JSON mẫu chứa dữ liệu phản hồi tại `Nexus.Gateway/Seeds/catalog/brands.json`:
```json
{
  "items": [
    { "id": "b-01", "name": "Keychron", "logoUrl": "https://example.com/keychron.png" },
    { "id": "b-02", "name": "Akko", "logoUrl": "https://example.com/akko.png" }
  ],
  "total": 2
}
```
*(Dữ liệu JSON sẽ được Protobuf parser tự động thẩm định cú pháp theo đúng schema trong `shop.proto`).*

### Bước 3: Tạo DTO & Thêm Action Trong Controller Tương Ứng
- Nếu có DTO mới $\rightarrow$ Thêm vào `Dtos/Catalog/CatalogRequests.cs` hoặc `CatalogResponses.cs`.
- Trong `Controllers/CatalogController.cs`, thêm Action tiêu chuẩn (gọi điều phối đúng 1 dòng):
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
        o => _client.GetBrandListAsync(new PageRequest { Page = page, PageSize = pageSize }, o), 
        BrandPage.Parser
    );
}
```

### Bước 4: Hoàn Tất! Không Cần Làm Gì Thêm
- **Cơ chế trung gian tự động**: Khi Client gọi tới `GET /gateway/api/v1/catalog/brands`:
  - Nếu `catalog` đang ở mode `Mock` $\rightarrow$ Tự động đọc file `Mocks/catalog/brands.json`, validate schema và trả về `ApiResponse<PagedData<Brand>>`.
  - Nếu `catalog` đang ở mode `Real` $\rightarrow$ Tự động chuyển tiếp gRPC tới backend `CatalogService:8002`, đính kèm JWT token, idempotency key và traceId.
- **Tự động hiện trên Swagger UI**: Endpoint mới tự động có mặt trên Swagger với schema tài liệu chuẩn chỉ.

---

## 4. CHI TIẾT BỘ ĐIỀU PHỐI TRUNG GIAN (`BackendDispatcher.cs`)

`BackendDispatcher` là trung gian cốt lõi chịu trách nhiệm kiểm tra trạng thái và ánh xạ request/response:

1. **Kiểm Tra Trạng Thái Hiệu Dụng (Effective Mode Resolution)**:
   - Truy vấn `IGatewayRuntimeManager` để lấy trạng thái của service đích (`Mock` hay `Real`).
   - Ghi nhận `X-Gateway-Mode` vào HTTP Response Header (`Mock` hoặc `Real`).
2. **Xử Lý Chế Độ Mock**:
   - Tự động nạp file fixture từ ổ đĩa `Mocks/{service}/{operation}.json`.
   - Parse qua `MessageParser<T>.ParseJson` $\rightarrow$ Đảm bảo dữ liệu mock luôn thỏa mãn 100% schema Protobuf (nếu dev gõ sai kiểu dữ liệu, hệ thống bắt lỗi ngay lập tức).
   - Áp dụng độ trễ giả lập (`MockDelayMs`) và mã lỗi giả lập (`MockStatus`) nếu có cấu hình.
   - Bọc dữ liệu vào envelope chuẩn `ApiResponse<T>` hoặc `ApiResponse<PagedData<T>>`.
3. **Xử Lý Chế Độ Real (gRPC Call)**:
   - Tự động forward Metadata: `Authorization` (JWT token), `Idempotency-Key`, `x-trace-id`.
   - Áp đặt thời hạn xử lý `Deadline` theo `TimeoutSeconds`.
   - Gọi hàm gRPC client thông qua kênh HTTP/2 được quản lý bởi `AddGrpcClient`.
   - Parse response Protobuf thành JsonElement và bọc vào envelope chuẩn.
4. **Phân Tán Trace (OpenTelemetry Tracing)**:
   - Tự động sinh `Activity` mang tên `${service}.${operation}` gắn kèm tags `backend.service` và `backend.mode`.

---

## 5. BẢNG TỔNG HỢP ROUTE VÀ DANH MỤC 10 CONTROLLERS

Toàn bộ các endpoint trong Gateway đều bắt đầu bằng tiền tố: `/gateway/api/v1/...`

| STT | Controller | File Code | Route Prefix | Quyền Hạn (RBAC) | Chức Năng Nghiệp Vụ | Service Đích |
|:---:|:---|:---|:---|:---|:---|:---:|
| 1 | `AuthController` | `Controllers/AuthController.cs` | `/gateway/api/v1/auth` | Anonymous | Đăng ký tài khoản (`/register`), Đăng nhập (`/login`). | `users` |
| 2 | `UsersController` | `Controllers/UsersController.cs` | `/gateway/api/v1/users/me` | Authenticated | Xem thông tin cá nhân (`/`), cập nhật hồ sơ, yêu cầu nâng cấp người bán (`/seller-upgrade`). | `users` |
| 3 | `CatalogController` | `Controllers/CatalogController.cs` | `/gateway/api/v1/catalog` | Anonymous / Public | Xem danh mục (`/categories`), xem danh sách sản phẩm (`/products`), xem chi tiết sản phẩm (`/products/{id}`). Admin tạo/sửa/xóa danh mục. | `catalog` |
| 4 | `CatalogController` | `Controllers/CatalogController.cs` | `/gateway/api/v1/seller` | Seller / Admin | Quản lý kho ảo seller (`/inventory`, `/inventory/adjust`), seller quản lý sản phẩm (`/products`). | `catalog` |
| 5 | `CommerceController` | `Controllers/CommerceController.cs` | `/gateway/api/v1/commerce/cart` | Authenticated | Thao tác giỏ hàng: xem giỏ (`/`), thêm hàng (`/items`), sửa số lượng, xóa sản phẩm, xóa trắng giỏ. | `commerce` |
| 6 | `CommerceController` | `Controllers/CommerceController.cs` | `/gateway/api/v1/commerce/orders` | Authenticated | Tạo đơn hàng checkout (`/`), xem danh sách đơn của tôi, xem chi tiết đơn hàng theo ID. | `commerce` |
| 7 | `CommerceController` | `Controllers/CommerceController.cs` | `/gateway/api/v1/seller/orders` | Seller | Xem danh sách đơn hàng thuộc quyền quản lý của shop. | `commerce` |
| 8 | `CommerceController` | `Controllers/CommerceController.cs` | `/gateway/api/v1/admin/orders` | Staff / Admin | Xem toàn bộ đơn hàng của toàn sàn thương mại điện tử. | `commerce` |
| 9 | `UsersController` | `Controllers/UsersController.cs` | `/gateway/api/v1/admin` | Staff / Admin | Xem danh sách người dùng (`/users`), duyệt hồ sơ seller (`/sellers/{id}/approve`), gán role, xem roles/permissions. | `users` |
| 10 | `AuditController` | `Controllers/AuditController.cs` | `/gateway/api/v1/audit` | Staff / Admin | Tra cứu nhật ký kiểm toán hệ thống (`/logs`), xem chi tiết log, xem danh mục sự kiện kiểm toán (`/event-types`), bật/tắt event type. | `audit` |
| 11 | `AuctionController` | `Controllers/AuctionController.cs` | `/gateway/api/v1/auctions` | Public / Seller / Auth | Xem danh sách đấu giá (`/`), xem chi tiết, seller tạo phiên đấu giá mới, người mua đặt giá trả (`/bids`), xem lịch sử giá bid. | `auction` |
| 12 | `FulfillmentController` | `Controllers/FulfillmentController.cs`| `/gateway/api/v1/fulfillment` | Anonymous / Public | Tra cứu tồn kho kho bãi vật lý (`/inventory/{skuId}`), tính phí giao hàng (`/shipping/quote`), tra cứu mã vận đơn (`/shipments/{code}`). | `fulfillment` |
| 13 | `NotificationController`| `Controllers/NotificationController.cs`| `/gateway/api/v1/notifications`| Authenticated | Xem danh sách thông báo cá nhân (`/`), đánh dấu đã đọc (`/read`), xem/cập nhật tùy chọn nhận thông báo (`/preferences`). | `notification` |
| 14 | `WalletController` | `Controllers/WalletController.cs` | `/gateway/api/v1/wallet` | Authenticated | Quản lý ví điện tử: tra cứu số dư (`/balance`), nạp tiền (`/deposit`), rút tiền (`/withdraw`), chuyển tiền nội bộ (`/transfer`), xem lịch sử giao dịch. | `wallet` |
| 15 | `GatewayControlController`| `Controllers/GatewayControlController.cs`| `/gateway/api/v1/gateway` | GatewayAdmin (chặn bypass)| Tra cứu trạng thái 8 services (`GET /modes`), đổi auth mode (`PUT /mode`), đổi mode Mock/Real từng service (`PUT /modes/{service}`). | *Nội bộ Gateway* |
| 16 | `GatewayControlController`| `Controllers/GatewayControlController.cs`| `/gateway/api/v1/mock-engine`| GatewayAdmin (chặn bypass)| Xem danh sách declarative mock endpoints (`GET /definitions`), kích hoạt nạp lại mock nóng (`POST /reload`). | *Nội bộ Gateway* |

---

## 6. BẢO MẬT, XÁC THỰC & TIỆN ÍCH DÀNH CHO LẬP TRÌNH VIÊN

1. **Chế Độ `AuthMode: Standard` (Mặc định)**:
   - Mọi request yêu cầu đăng nhập bắt buộc phải mang JWT Token hợp lệ trên header `Authorization: Bearer <token>`.
   - Nếu token không đủ quyền (ví dụ role `buyer` gọi API quản trị `/gateway/api/v1/admin/users`) $\rightarrow$ Bị chặn ngay lập tức với mã HTTP `403 Forbidden`.

2. **Chế Độ `AuthMode: Bypass` (Tiện ích độc quyền Development)**:
   - Dành riêng cho Frontend test giao diện và mock data độc lập.
   - Khi bật `Bypass`, lập trình viên gọi bất kỳ API Mock nào (kể cả xem giỏ hàng, thông tin ví cá nhân hay trang quản trị admin) mà **không cần đăng nhập hay mang token**, Gateway sẽ tự động gán danh tính giả lập tương ứng để vượt qua bộ lọc RBAC một cách mượt mà.

3. **Mock Token Trên Swagger UI**:
   - Khi chạy ở môi trường Development, nút xanh **Authorize** trên Swagger UI cho phép bạn chỉ cần gõ tên vai trò (`admin`, `seller`, `staff`, hoặc `buyer`), Gateway sẽ tự động phát sinh ClaimsPrincipal tương ứng mà không cần phải gọi API login.

4. **An Toàn Tuyệt Đối**:
   - Khi chuyển một service sang chế độ `Real`, cơ chế Mock Token lập tức bị vô hiệu hóa cho service đó và bắt buộc phải dùng JWT thật từ `UsersService`.
   - Ở môi trường `Production`, cơ chế Bypass và Mock Auth bị tắt hoàn toàn trong code.

---

## 7. KIỂM THỬ TỰ ĐỘNG & BẢO ĐẢM CHẤT LƯỢNG

Toàn bộ 46/46 unit & integration test cases đã được cập nhật đồng bộ với tiền tố `/gateway/api/v1/...` và kiến trúc module hóa mới:
- **`ApiResponseFactoryTests.cs`**: Đảm bảo envelope thành công và lỗi RFC 9110 luôn chuẩn chỉ.
- **`GatewayTests.cs`**: Kiểm tra Kestrel limits (64KB), timeout 65s, và Rate Limiting phân tán qua Redis.
- **`MockEngineTests.cs`**: Kiểm tra thuật toán khớp route động, ưu tiên route tĩnh, và nạp mock JSON.
- **`GatewaySurfaceTests.cs`**: Kiểm tra toàn bộ bề mặt API của 8 services.
- **`SevenServicesGatewayTests.cs`**: Kiểm tra chi tiết luồng nghiệp vụ thực tế của cả 8 microservices.

### Lệnh chạy kiểm thử:
```bash
dotnet test src/Services/ApiGateway/ApiGateway.sln -c Release
```
*(Trạng thái hiện tại: **Passed! Failed: 0, Passed: 46, Skipped: 0, Total: 46, Duration: ~5s**).*
