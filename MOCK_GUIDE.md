# HƯỚNG DẪN TẠO MOCK API TẠI NEXUS GATEWAY
## (QUY CHUẨN ĐỒNG NHẤT CHO TOÀN BỘ ĐỘI NGŨ PHÁT TRIỂN)
> **Trạng thái triển khai (2026-10-30):** ✅ ĐÃ CHUẨN HÓA — **một cơ chế mock duy nhất**: Controller Action + Seed Fixture trong `Seeds/`. Cơ chế declarative mock (`Mocks/*.mock.json` + `MockEngine/`) đã bị gỡ bỏ hoàn toàn. 15/15 controller có action (177 action).
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng mục).
> **Cập nhật lần cuối:** 2026-10-30.
> **Mục tiêu**: Hướng dẫn developer (Frontend, Backend, QA) nắm vững luồng xử lý thực tế của API Gateway và quy trình tạo Mock API: **Khai báo Endpoint trong Controller C# + Tạo dữ liệu Seed Fixture tại thư mục `Seeds/`**.
---
## 🏛️ 1. Kiến Trúc & Các Bước Tiền Xử Lý Của Gateway (Request Pipeline)
Mọi request khi gửi đến Nexus API Gateway đều được xử lý tuần tự trong middleware pipeline thật (`Extensions/PipelineExtensions.cs`):
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
5. UseRequestTimeouts (65s)
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
   - Cấp phát mã `X-Trace-Id` duy nhất xuyên suốt vòng đời request; OTel exporter gửi span tới `OTEL_EXPORTER_OTLP_ENDPOINT` (`Extensions/TelemetryExtensions.cs`).
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
   - Controller validate qua helper (`ValidationHelpers`, `PaginationRequest.EnsureValid()`), ném `GatewayInputException`.
6. **Gateway Dispatcher Execution (`IGatewayDispatcher`)**:
   - **`Mock`**: đọc file seed trong `Seeds/{service}/{operation}.json`, bọc trong envelope `ApiResponse<T>` hoặc `ApiResponse<PagedData<T>>`.
   - **`Real`**: kiểm tra Circuit Breaker từ Health Prober; nếu healthy gọi REST downstream tới service backend tương ứng qua `HttpBackendCaller` (kế thừa `GatewayBaseController.ForwardAsync`).

Không còn bước nào khác chèn giữa routing và controller. Mọi route không khớp Controller Action đều trả **404** — Gateway không tự forward route lạ sang backend.
---
## 2. Cơ Chế Mock Duy Nhất: Controller Action + `Seeds/`
| Cơ chế | Khi nào dùng | Dữ liệu thật | Trạng thái | Bằng chứng |
|:---|:---|:---|:---|:---|
| **Controller Action + `Seeds/`** | Mọi endpoint. Kế thừa `GatewayBaseController`, dùng `ForwardAsync` / `ForwardPagedAsync` / `ForwardNoContentAsync`; đầy đủ DTO binding, `[Authorize]`, rate limit, envelope chuẩn | 163 file seed phủ 9 nhóm service | ✅ CÓ | `Dispatching/MockDataMapper.cs`, `Controllers/GatewayBaseController.cs` |

**Vì sao dùng Controller Action + Seed Fixture:**
1. **Đầy đủ các bước bảo vệ**: có DTO binding, validation C#, `[Authorize]`, `[EnableRateLimiting]`.
2. **Chuyển `Mock` → `Real` trong 0 giây (Zero-Code Switch)**: controller là cầu nối REST downstream; chỉ cần đổi `"Mode": "Real"` trong `gateway.runtime.json` hoặc cấu hình môi trường.
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
[Route("api/v1")]
[BackendTag("catalog")]
[EnableRateLimiting("api")]
public sealed class ProductsController : GatewayBaseController
{
    private const string Service = "catalog";

    public ProductsController(IGatewayDispatcher dispatcher) : base(dispatcher)
    {
    }

    /// <summary>Lists products with pagination.</summary>
    [HttpGet("products")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PagedData<ProductDto>>), StatusCodes.Status200OK)]
    public Task<IResult> ListProducts([FromQuery] PaginationRequest pagination, [FromQuery] string? keyword = null)
    {
        pagination.EnsureValid();

        return ForwardPagedAsync(
            Service,
            "/api/v1/products",
            pagination.Page,
            pagination.PageSize,
            mockSeed: "products",
            queryParams: new Dictionary<string, string?> { ["keyword"] = keyword });
    }
}
```
> 💡 **Với API trả về một đối tượng đơn (Single Object) hoặc POST/PUT**:
> ```csharp
> [HttpPost("products")]
> [Authorize(GatewayPolicies.Seller)]
> [ProducesResponseType(typeof(ApiResponse<ProductDetailDto>), StatusCodes.Status201Created)]
> public Task<IResult> CreateProduct([FromBody] CreateProductRequestDto body)
> {
>     ValidationHelpers.EnsureRequired(body.Name, "Name");
>
>     return ForwardAsync(
>         Service,
>         "/api/v1/products",
>         HttpMethod.Post,
>         body: body,
>         mockSeed: "product-create",
>         successMessage: "Product created successfully.",
>         successStatusCode: StatusCodes.Status201Created);
> }
> ```
---
### 👉 Bước 2: Tạo file JSON dữ liệu mẫu trong `Seeds/`
Tạo file dữ liệu mẫu theo đúng quy tắc đường dẫn:
```
Nexus.Gateway/Seeds/{tên_service}/{tên_operation}.json
```
*(Dự phòng: `Nexus.Gateway/Seeds/{tên_operation}/{tên_operation}.json` — `MockDataMapper.ResolveSeedFilePath` thử lần lượt các đường dẫn ứng viên.)*

Hiện trạng thật (2026-10-30): **163 file JSON** — `users` (60), `catalog` (28), `commerce` (20), `auction` (18), `notification` (13), `fulfillment` (8), `wallet` (7), `log` (5), `auth` (4).

*Ví dụ:* Với `service: "catalog"`, `operation: "products"`, tạo `Nexus.Gateway/Seeds/catalog/products.json`:
```json
{
  "items": [
    {
      "id": "22222222-2222-2222-2222-000000000001",
      "name": "Wireless mechanical keyboard Nexus Pro",
      "price": 1250000,
      "status": "Active"
    }
  ],
  "total": 1
}
```
> Mọi chuỗi dữ liệu mẫu nên là tiếng Anh để khớp quy chuẩn thông báo/response (Nguyên tắc 5 trong [AGENTS.md](AGENTS.md)); dữ liệu nghiệp vụ thuần hiển thị có thể giữ ngôn ngữ sản phẩm.

Mọi `mockSeed` khai báo trong controller **bắt buộc** phải có file tương ứng. `MockDataMapper` trả `SEED_DATA_NOT_FOUND` (HTTP 500) khi thiếu fixture.
---
### 👉 Bước 3: Đảm bảo Service đặt `Mode: "Mock"` và Test trên Swagger
1. Kiểm tra cấu hình trong `Nexus.Gateway/gateway.runtime.json`:
   ```json
   "Services": {
     "catalog": {
       "Mode": "Mock",
       "Address": "http://localhost:8002",
       "TimeoutSeconds": 5
     }
   }
   ```
2. Mở trình duyệt: `http://localhost:8080/swagger` (Development).
3. Tìm endpoint vừa khai báo → **Try it out** → **Execute**.
4. Gateway trả response bọc trong `ApiResponse<T>`:
   ```json
   {
     "success": true,
     "statusCode": 200,
     "message": "Products retrieved successfully.",
     "data": {
       "items": [],
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
     "timestamp": "2026-10-30T14:30:00Z"
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
  "timestamp": "2026-10-30T14:30:00Z"
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
  "timestamp": "2026-10-30T14:30:00Z"
}
```
> ⚠️ **Lưu ý quy tắc thông báo lỗi (Error Rules)**:
> - **Ngôn ngữ**: 100% tiếng Anh (`English only`).
> - **Quy cách**: Viết hoa duy nhất chữ cái đầu câu (`Sentence case`), ví dụ: `"Email address is required."`.
> - **Mã lỗi**: Viết hoa phân cách gạch dưới (`SCREAMING_SNAKE_CASE`), ví dụ: `VAL_INVALID_INPUT`, `USER_NOT_FOUND`, `AUTH_UNAUTHORIZED`.
---
## ⚡ 5. Các Mẹo Tiện Ích Dành Cho Developer
### 1. Hot-Reload dữ liệu Seed tức thì — ✅ CÓ
- `MockDataMapper` đọc file seed bằng `File.ReadAllTextAsync` **mỗi request** (không cache). Sửa nội dung JSON rồi gọi lại API là thấy dữ liệu mới, không cần restart.
### 2. Bỏ qua xác thực để test nhanh giao diện (Bypass Auth Mode) — ⚠️ chỉ Development
Khi cần test nhanh frontend mà chưa có JWT:
1. Mở `Nexus.Gateway/gateway.runtime.json`.
2. Đổi `"AuthMode": "Bypass"`.
3. Lưu file → các API `[Authorize]` của service đang `Mock` không bị chặn 401/403. Chỉ áp dụng khi `ASPNETCORE_ENVIRONMENT=Development`; production sẽ fail-fast khi khởi động (`GatewayOptions.IsValid`).
### 3. Chuyển đổi giữa Mock và Real cho từng Service — ✅ CÓ
- Khi backend service (ví dụ Catalog, port 8002) đã hoàn thiện:
  - Sửa `gateway.runtime.json`: `"catalog": { "Mode": "Real" }`.
  - Hoặc gọi API quản trị (cần JWT Gateway admin): `PUT /api/v1/gateway/modes/catalog` body `{"mode": "Real"}`.
### 4. Ghi đè Mode cho riêng 1 Request (Header Override) — ✅ CÓ (Development)
- Khi service đang `Mock`, test 1 request sang backend thật bằng header:
  ```http
  X-Service-Mode-catalog: Real
  ```
  Chỉ hoạt động khi `AllowClientModeOverrideInDevelopment=true` (đang bật trong `gateway.runtime.json`).
