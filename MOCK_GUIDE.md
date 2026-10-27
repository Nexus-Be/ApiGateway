# HƯỚNG DẪN TẠO MOCK API TẠI NEXUS GATEWAY
## (QUY CHUẨN ĐỒNG NHẤT CHO TOÀN BỘ ĐỘI NGŨ PHÁT TRIỂN)
> **Trạng thái triển khai (2026-10-24):** ⚠️ Một phần — hai cơ chế mock cùng chạy (seed fixture 9 file cho `auth`+`users`, declarative 6 file `*.mock.json`); 11/15 controller vẫn là stub nên phần lớn route chưa có action.
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-24.
> **Mục tiêu**: Hướng dẫn developer (Frontend, Backend, QA) nắm vững luồng xử lý thực tế của API Gateway và quy trình tạo Mock API: **Khai báo Endpoint trong Controller C# + Tạo dữ liệu Seed Fixture tại thư mục `Seeds/`** (khuyến nghị mặc định), hoặc **khai báo declarative mock `Mocks/*.mock.json`** cho route chưa có action.
---
## 🏛️ 1. Kiến Trúc & Các Bước Tiền Xử Lý Của Gateway (Request Pipeline)
Mọi request khi gửi đến Nexus API Gateway đều được xử lý tuần tự qua **8 bước trong middleware pipeline** thật (`Extensions/PipelineExtensions.cs`):
```
[ Client Request ]
       │
       ▼
1. Exception Handler + StatusCodePages + Serilog RequestLogging (cấp phát X-Trace-Id)
       │
       ▼
2. Header Gateway (X-Trace-Id, X-Content-Type-Options, X-Gateway-Mode) + URL Rewrite /gateway/api/* -> /api/*
       │
       ▼
3. UseRouting -> UseCors -> GatewayRequestResolution (snapshot Mode/AuthMode cho request)
       │
       ▼
4. MockSecurityAdapter -> UseAuthentication -> RedisRateLimit -> UseRateLimiter -> UseAuthorization
       │
       ▼
5. UseMockEngine (declarative mock cho route không có Controller Action) -> UseRequestTimeouts (65s)
       │
       ▼
6. Controller Model Binding & Input Validation (Bind DTO, validate)
       │
       ▼
7. GatewayDispatcher Execution (IGatewayDispatcher: Mock -> Seeds/ | Real -> REST downstream qua HttpBackendCaller)
       │
       ▼
8. Health endpoints /health/live, /health/ready + Swagger (Development)
       │
       ▼
[ Client Response: ApiResponse<T> ]
```
### Chi tiết các bước xử lý:
1. **Global Exception Handling & Diagnostics**:
   - `GatewayExceptionHandler` bắt toàn bộ lỗi, chuyển đổi sang chuẩn **RFC 9110 ProblemDetails** và bọc trong envelope `ApiErrorResponse`.
   - Cấp phát mã `X-Trace-Id` duy nhất xuyên suốt vòng đời request; OTel exporter gửi span tới `OTEL_EXPORTER_OTLP_ENDPOINT` (`Extensions/TelemetryExtension.cs`).
2. **URL Rewrite & Route Resolution**:
   - Chuẩn hóa `/gateway/api/v1/...` về `/api/v1/...` để tương thích ngược.
   - Nhận diện service qua `[BackendTag("service_name")]` trên Controller và khớp route với Controller Action.
3. **Gateway Request Resolution & Mode Evaluation**:
   - Đọc cấu hình `gateway.runtime.json` (nạp sau `appsettings.{Environment}.json`) để xác định `Mode` (`Mock`/`Real`) và `AuthMode` (`Enforce`/`Standard` vs `Bypass` — Bypass chỉ Development).
4. **Security, Authentication, RBAC & Rate Limiting**:
   - JWT (`UseAuthentication`) và phân quyền (`UseAuthorization`) theo `[Authorize]`/`[AllowAnonymous]`.
   - Giới hạn tần suất qua Redis middleware + policy `[EnableRateLimiting("api")]`.
5. **Model Binding & Input Validation**:
   - ASP.NET Core bind query/route/body vào DTO.
   - Controller validate qua helper (`ValidationHelpers`, `pagination.EnsureValid()`), ném `GatewayInputException`.
6. **Gateway Dispatcher Execution (`IGatewayDispatcher`)**:
   - **`Mock`**: đọc file seed trong `Seeds/{service}/{operation}.json`, bọc trong envelope `ApiResponse<T>` hoặc `ApiResponse<PagedData<T>>`.
   - **`Real`**: kiểm tra Circuit Breaker từ Health Prober; nếu healthy gọi REST downstream tới service backend tương ứng qua `HttpBackendCaller` (kế thừa `GatewayBaseController.ForwardAsync`).
---
## 2. Hai Cơ Chế Mock Đang Chạy Song Song
| Cơ chế | Khi nào dùng | Dữ liệu thật | Trạng thái | Bằng chứng |
|:---|:---|:---|:---|:---|
| **Controller Action + `Seeds/`** (khuyến nghị số 1) | Kế thừa `GatewayBaseController`, dùng `ForwardAsync` / `ForwardPagedAsync`; đầy đủ DTO binding, `[Authorize]`, rate limit, envelope chuẩn | 46 file seed phủ khắp 8 backend services | ✅ CÓ | `Dispatching/MockDataMapper.cs`, `Controllers/GatewayBaseController.cs` |
| **Declarative `Mocks/*.mock.json`** | Route **chưa có** `ControllerActionDescriptor` nào khớp và service đang `Mock` | 6 file | ✅ CÓ | `MockEngine/MockEngineMiddleware.cs:30-51` |
Cơ chế ưu tiên (đọc trực tiếp từ `MockEngineMiddleware.cs:49-52`):
```csharp
if (effectiveMode == "Real" || context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>() != null)
{
    await _next(context);
    return;
}
```
Nghĩa là: nếu route đã có Controller Action, declarative mock **bị bỏ qua** và Controller xử lý (đọc `Seeds/`). Nếu service chuyển sang `Real` mà route **không có** Controller Action tương ứng, request trả **404** — không tự forward sang backend.
**Vì sao nên tạo Controller Action + Seed Fixture:**
1. **Đầy đủ các bước bảo vệ**: có DTO binding, validation C#, `[Authorize]`, `[EnableRateLimiting]`.
2. **Chuyển `Mock` $\rightarrow$ `Real` trong 0 giây (Zero-Code Switch)**: controller là cầu nối REST downstream; chỉ cần đổi `"Mode": "Real"` trong `gateway.runtime.json` hoặc cấu hình môi trường.
3. **Response chuẩn**: envelope `ApiResponse<T>` + `X-Gateway-Mode` header do Controller/Dispatcher thống nhất.
---
## 🚀 3. Quy Trình 3 Bước Tạo Mock API Chuẩn Tại Gateway
```
[ Bước 1: Viết Action trong Controller kế thừa GatewayBaseController ] ──► [ Bước 2: Tạo Seed JSON trong Seeds/ ] ──► [ Bước 3: Đặt Mode: Mock & Test ]
```
### 👉 Bước 1: Khai báo Action trong Controller kế thừa `GatewayBaseController`
Mở Controller tại `Nexus.Gateway/Controllers/` (ví dụ `ProductsController.cs`):
```csharp
[ApiController]
[Route("api/v1/catalog/products")]
[BackendTag("catalog")]
[EnableRateLimiting("api")]
public sealed class ProductsController : GatewayBaseController
{
    public ProductsController(IGatewayDispatcher dispatcher) : base(dispatcher)
    {
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PagedData<ProductDto>>), StatusCodes.Status200OK)]
    public Task<IResult> ListProducts([FromQuery] PaginationRequest pagination)
    {
        // 1. Tự động validate phân trang: Page >= 1, PageSize từ 1-100
        pagination.EnsureValid();

        // 2. ForwardPagedAsync tự động điều phối:
        //    - Mode = Mock: đọc Seeds/catalog/products.json
        //    - Mode = Real: gọi REST GET /api/v1/products?page=... sang Catalog Service
        return ForwardPagedAsync<ProductDto>(new()
        {
            Service = "catalog",
            DownstreamPath = "/api/v1/products",
            Page = pagination.Page,
            PageSize = pagination.PageSize,
            MockSeed = "products"
        });
    }
}
```
> 💡 **Với API trả về một đối tượng đơn (Single Object) hoặc POST/PUT**:
> ```csharp
> [HttpPost]
> [Authorize("seller")]
> [ProducesResponseType(typeof(ApiResponse<ProductDetailDto>), StatusCodes.Status201Created)]
> public Task<IResult> CreateProduct([FromBody] CreateProductRequestDto body)
> {
>     ValidationHelpers.EnsureRequired(body.Name, "Name");
>
>     return ForwardAsync<CreateProductRequestDto, ProductDetailDto>(new()
>     {
>         Service = "catalog",
>         DownstreamPath = "/api/v1/products",
>         Method = HttpMethod.Post,
>         Body = body,
>         MockSeed = "product-create",
>         SuccessMessage = "Product created successfully.",
>         SuccessStatusCode = StatusCodes.Status201Created
>     });
> }
> ```
>         ProductResponse.Parser,
>         successMessage: "Product created successfully.",
>         successStatusCode: StatusCodes.Status201Created
>     );
> }
> ```
---
### 👉 Bước 2: Tạo file JSON dữ liệu mẫu trong `Seeds/`
Tạo file dữ liệu mẫu theo đúng quy tắc đường dẫn:
```
Nexus.Gateway/Seeds/{tên_service}/{tên_operation}.json
```
*(Dự phòng: `Nexus.Gateway/Seeds/{tên_operation}/{tên_operation}.json` — `MockDataMapper` thử lần lượt các đường dẫn ứng viên.)*
Hiện trạng thật: `Seeds/auth/` có 4 file (`login`, `register`, `verify-otp`, `resend-otp`), `Seeds/users/` có 5 file (`login`, `profile`, `register`, `verify-otp`, `resend-otp`); `Seeds/cart|orders|products/` mới chỉ có `README.md`.
*Ví dụ (đặc tả mục tiêu):* Với `service: "catalog"`, `operation: "products"`, tạo `Nexus.Gateway/Seeds/catalog/products.json`:
```json
{
  "items": [
    {
      "id": "22222222-2222-2222-2222-000000000001",
      "name": "Wireless mechanical keyboard Nexus Pro",
      "price": 1250000,
      "status": "Active"
    },
    {
      "id": "22222222-2222-2222-2222-000000000002",
      "name": "Nexus X silent gaming mouse",
      "price": 650000,
      "status": "Active"
    }
  ],
  "total": 2
}
```
> Mọi chuỗi dữ liệu mẫu nên là tiếng Anh để khớp quy chuẩn thông báo/response (Nguyên tắc 5 trong [AGENTS.md](AGENTS.md)); dữ liệu nghiệp vụ thuần hiển thị có thể giữ ngôn ngữ sản phẩm.
---
### 👉 Bước 3: Đảm bảo Service đặt `Mode: "Mock"` và Test trên Swagger
1. Kiểm tra cấu hình trong `Nexus.Gateway/gateway.runtime.json` (file này **được track trong git**):
   ```json
   "Services": {
     "catalog": {
       "Mode": "Mock",
       "Address": "http://localhost:8002",
       "TimeoutSeconds": 5
     }
   }
   ```
   Giá trị đang lưu trong repo: `users` và `auction` = `Mock`, `catalog` và 5 service còn lại = `Real`.
2. Mở trình duyệt: `http://localhost:8000/swagger` (Development).
3. Tìm endpoint vừa khai báo $\rightarrow$ **Try it out** $\rightarrow$ **Execute**.
4. Gateway trả response bọc trong `ApiResponse<T>`:
   ```json
   {
     "success": true,
     "statusCode": 200,
     "message": "Products retrieved successfully.",
     "data": {
       "items": [
         {
           "id": "22222222-2222-2222-2222-000000000001",
           "name": "Wireless mechanical keyboard Nexus Pro",
           "price": 1250000,
           "status": "Active"
         }
       ],
       "pagination": {
         "pageIndex": 1,
         "pageSize": 20,
         "totalCount": 2,
         "totalPages": 1,
         "hasPreviousPage": false,
         "hasNextPage": false
       }
     },
     "traceId": "0HNPGT7G285Q2",
     "timestamp": "2026-10-24T14:30:00Z"
   }
   ```
---
## 📋 4. Quy Chuẩn Response & Xử Lý Lỗi Hệ Thống
Toàn bộ API qua Gateway bắt buộc tuân theo chuẩn response đồng nhất:
### Phản hồi thành công (`ApiResponse<T>`):
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Sentence case success message in 100% English.",
  "data": { },
  "traceId": "0HNPGT7G285Q2",
  "timestamp": "2026-10-24T14:30:00Z"
}
```
### Phản hồi thất bại / Lỗi (`ApiErrorResponse` theo RFC 9110 ProblemDetails):
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errorCode": "VAL_INVALID_INPUT",
  "detail": "Username, valid email, and password with at least 8 characters are required.",
  "errors": {
    "Email": ["Email address is invalid."]
  },
  "traceId": "0HNPGT7G285Q2",
  "timestamp": "2026-10-24T14:30:00Z"
}
```
> ⚠️ **Lưu ý quy tắc thông báo lỗi (Error Rules)**:
> - **Ngôn ngữ**: 100% tiếng Anh (`English only`).
> - **Quy cách**: Viết hoa duy nhất chữ cái đầu câu (`Sentence case`), ví dụ: `"Email address is required."`.
> - **Mã lỗi**: Viết hoa phân cách gạch dưới (`SCREAMING_SNAKE_CASE`), ví dụ: `VAL_INVALID_INPUT`, `USER_NOT_FOUND`, `AUTH_UNAUTHORIZED`.
> - Hiện trạng vi phạm: 3 chuỗi tiếng Việt còn trong `Common/ValidationHelpers.cs:23,29` và `Common/MoneyPrecision.cs:18` ⚠️.
---
## ⚡ 5. Các Mẹo Tiện Ích Dành Cho Developer
### 1. Hot-Reload dữ liệu Seed tức thì — ✅ CÓ
- `MockDataMapper` đọc file seed bằng `File.ReadAllTextAsync` **mỗi request** (không cache — `MockDataMapper.cs:64-76,126-138`). Sửa nội dung JSON rồi gọi lại API là thấy dữ liệu mới, không cần restart.
- Với declarative mock: gọi `POST /api/v1/gateway/mock-engine/reload` sau khi sửa file, hoặc xem danh sách đang nạp qua `GET /api/v1/gateway/mock-engine/definitions`.
### 2. Bỏ qua xác thực để test nhanh giao diện (Bypass Auth Mode) — ⚠️ chỉ Development
Khi cần test nhanh frontend mà chưa có JWT:
1. Mở `Nexus.Gateway/gateway.runtime.json`.
2. Đổi `"AuthMode": "Bypass"` (giá trị đang lưu trong repo là `"Enforce"`).
3. Lưu file $\rightarrow$ các API `[Authorize]` của service đang `Mock` không bị chặn 401/403. Chỉ áp dụng khi `ASPNETCORE_ENVIRONMENT=Development`; production sẽ fail-fast khi khởi động (`GatewayOptions.IsValid`).
### 3. Chuyển đổi giữa Mock và Real cho từng Service — ✅ CÓ
- Khi backend service (ví dụ Catalog gRPC port 8002) đã hoàn thiện — lưu ý hiện **chưa có** typed client Catalog:
  - Sửa `gateway.runtime.json`: `"catalog": { "Mode": "Real" }`.
  - Hoặc gọi API quản trị (cần JWT Gateway admin): `PUT /api/v1/gateway/modes/catalog` body `{"mode": "Real"}`.
### 4. Ghi đè Mode cho riêng 1 Request (Header Override) — ✅ CÓ (Development)
- Khi service đang `Mock`, test 1 request sang backend thật bằng header:
  ```http
  X-Service-Mode-catalog: Real
  ```
  Chỉ hoạt động khi `AllowClientModeOverrideInDevelopment=true` (đang bật trong `gateway.runtime.json`).