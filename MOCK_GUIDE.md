# HƯỚNG DẪN TẠO MOCK API TẠI NEXUS GATEWAY
## (QUY CHUẨN ĐỒNG NHẤT CHO TOÀN BỘ ĐỘI NGŨ PHÁT TRIỂN)

> **Mục tiêu**: Hướng dẫn developer (Frontend, Backend, QA) nắm vững luồng xử lý thực tế của API Gateway và quy trình chuẩn **DUY NHẤT** để tạo một Mock API hoàn chỉnh: **Khai báo Endpoint trong Controller C# + Tạo dữ liệu Seed Fixture tại thư mục `Seeds/`**.

---

## 🏛️ 1. Kiến Trúc & Các Bước Tiền Xử Lý Của Gateway (Request Pipeline)

Mọi request khi gửi đến Nexus API Gateway đều được xử lý tuần tự và tự động qua **6 bước trong Middleware Pipeline** (`PipelineExtensions.cs`) trước khi trả về cho client:

```
[ Client Request ]
       │
       ▼
1. Exception Handler & Centralized Logging (Bắt ngoại lệ toàn cục, cấp phát X-Trace-Id)
       │
       ▼
2. URL Rewrite & Route Resolution (/gateway/api/* -> /api/*, ánh xạ tới Controller Action)
       │
       ▼
3. Mode & Auth Resolution (Xác định Service Mode: "Mock"/"Real", AuthMode: "Enforce"/"Bypass")
       │
       ▼
4. Security & Rate Limiting (JWT Bearer Token, [Authorize] RBAC, Redis Rate Limiting)
       │
       ▼
5. Controller Model Binding & Input Validation (Bind DTO, tự động validate dữ liệu đầu vào)
       │
       ▼
6. Gateway Dispatcher Execution (IGatewayDispatcher điều phối: Mock -> Seeds/ | Real -> gRPC)
       │
       ▼
[ Client Response: ApiResponse<T> ]
```

### Chi tiết 6 bước xử lý:
1. **Global Exception Handling & Diagnostics**:
   - `GatewayExceptionHandler` bắt toàn bộ lỗi, tự động chuyển đổi sang chuẩn **RFC 9110 ProblemDetails** và bọc trong envelope lỗi chuẩn (`ApiErrorResponse`).
   - Cấp phát mã `X-Trace-Id` duy nhất xuyên suốt toàn bộ vòng đời request và đồng bộ sang Grafana Tempo.
2. **URL Rewrite & Route Resolution**:
   - Tự động chuẩn hóa `/gateway/api/v1/...` về `/api/v1/...` để đảm bảo tương thích ngược.
   - Nhận diện Service phụ trách thông qua attribute `[BackendTag("service_name")]` trên Controller và khớp Route với Controller Action.
3. **Gateway Request Resolution & Mode Evaluation**:
   - `GatewayRequestResolution` kiểm tra cấu hình trong `gateway.runtime.json` để xác định trạng thái của Service (`Mock` hay `Real`) và AuthMode (`Enforce` hay `Bypass`).
4. **Security, Authentication, RBAC & Rate Limiting**:
   - Kiểm tra JWT Token (`UseAuthentication`) và phân quyền người dùng (`UseAuthorization`) dựa trên metadata `[Authorize(Roles = "...")]` hoặc `[AllowAnonymous]`.
   - Kiểm tra giới hạn tần suất gọi API qua Redis Distributed Rate Limiting (`[EnableRateLimiting]`).
5. **Model Binding & Input Validation**:
   - ASP.NET Core tự động bind query parameters, route values và request body vào DTO C#.
   - Controller validate logic nghiệp vụ qua các helper (`ValidationHelpers`, `pagination.EnsureValid()`), ném `GatewayInputException` khi dữ liệu không hợp lệ.
6. **Gateway Dispatcher Execution (`IGatewayDispatcher`)**:
   - **Khi Service ở chế độ `Mock`**: Tự động tìm và đọc file dữ liệu seed mẫu từ thư mục `Seeds/{service}/{operation}.json`, kiểm tra tính hợp lệ qua Protobuf Parser, và bọc trong `ApiResponse<T>` chuẩn.
   - **Khi Service ở chế độ `Real`**: Kiểm tra trạng thái Circuit Breaker từ Health Prober Daemon; nếu healthy sẽ gọi gRPC client sang Microservice downstream tương ứng và bọc kết quả trả về.

---

## ⚠️ 2. Tại Sao Hệ Thống Thống Nhất Duy Nhất 1 Cách Mock?

Trước đây có tài liệu nhắc đến 2 cách (tạo file declarative `*.mock.json` ở `Mocks/` hoặc viết Controller + `Seeds/`). Tuy nhiên, **thực tế kiến trúc của Gateway chỉ sử dụng và khuyến nghị DUY NHẤT một cách: Viết Controller Action + Tạo file Seed Fixture (`Seeds/`)**.

Lý do bắt buộc phải tạo Controller Endpoint kết hợp Seed Fixture:
1. **Đảm bảo đầy đủ các bước của Gateway**: Nếu chỉ tạo file JSON đứng riêng lẻ mà không có Controller, API sẽ không có DTO binding, không có C# validation, không thể khai báo các Attribute phân quyền (`[Authorize]`) và Rate Limit (`[EnableRateLimiting]`) theo chuẩn của hệ thống.
2. **Chuyển đổi `Mock` $\rightarrow$ `Real` trong 0 giây (Zero-Code Switch)**: Controller chính là cầu nối gRPC client. Khi backend service thật hoàn thành, bạn chỉ cần đổi cấu hình `"Mode": "Real"` trong `gateway.runtime.json`, Controller sẽ tự động gọi gRPC mà **không cần viết lại hay sửa đổi bất kỳ dòng code Gateway nào**. Nếu mock không có Controller, khi chuyển sang `Real` request sẽ bị **404 Not Found**.
3. **Cơ chế tự động ưu tiên của Gateway Middleware**: Trong code `MockEngineMiddleware.cs` (dòng 51):
   ```csharp
   if (effectiveMode == "Real" || context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>() != null)
   {
       await _next(context);
       return;
   }
   ```
   Hễ một route đã được định nghĩa Action trong C# Controller thì Gateway **ngay lập tức bỏ qua** file declarative mock để nhường quyền xử lý cho Controller và đọc dữ liệu từ `Seeds/`.

---

## 🚀 3. Quy Trình 3 Bước Tạo Mock API Chuẩn Tại Gateway

Mọi endpoint mới cần mock đều tuân thủ đúng 3 bước sau:

```
[ Bước 1: Viết Action trong Controller ] ──► [ Bước 2: Tạo Seed JSON trong Seeds/ ] ──► [ Bước 3: Đặt Mode: Mock & Test ]
```

### 👉 Bước 1: Khai báo Action trong Controller tương ứng
Mở Controller tương ứng tại thư mục `Nexus.Gateway/Controllers/` (ví dụ `ProductsController.cs`, `CartController.cs`, `OrdersController.cs`...):

```csharp
[ApiController]
[Route("api/v1/catalog/products")]
[BackendTag("catalog")]
[EnableRateLimiting("api")]
public sealed class ProductsController : ControllerBase
{
    private readonly IGatewayDispatcher _dispatcher;
    private readonly CatalogApi.CatalogApiClient _client;

    public ProductsController(IGatewayDispatcher dispatcher, CatalogApi.CatalogApiClient client)
    {
        _dispatcher = dispatcher;
        _client = client;
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PagedData<ProductDto>>), StatusCodes.Status200OK)]
    public Task<IResult> ListProducts([FromQuery] PaginationRequest pagination)
    {
        // 1. Tự động validate phân trang: Page >= 1, PageSize từ 1-100
        pagination.EnsureValid();

        // 2. Dispatcher tự động điều phối:
        //    - Mode = Mock: đọc Seeds/catalog/products.json
        //    - Mode = Real: gọi _client.ListProductsAsync(...)
        return _dispatcher.DispatchPagedAsync(
            HttpContext,
            "catalog",                                                          // Tên service
            "products",                                                         // Tên operation (tương ứng tên file seed)
            pagination.Page,
            pagination.PageSize,
            o => _client.ListProductsAsync(new ListProductsRequest              // gRPC call khi ở Real mode
            {
                Page = pagination.Page,
                PageSize = pagination.PageSize
            }, o),
            ProductPageResponse.Parser                                          // Protobuf Message Parser
        );
    }
}
```

> 💡 **Với API trả về một đối tượng đơn (Single Object) hoặc POST/PUT**:
> ```csharp
> [HttpPost]
> [Authorize("seller")]
> [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status201Created)]
> public Task<IResult> CreateProduct([FromBody] CreateProductRequestDto body)
> {
>     ValidationHelpers.EnsureRequired(body.Name, "Name");
> 
>     return _dispatcher.DispatchAsync(
>         HttpContext,
>         "catalog",
>         "product-create",
>         o => _client.CreateProductAsync(new CreateProductRequest { Name = body.Name }, o),
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
*(Hoặc `Nexus.Gateway/Seeds/{tên_operation}/{tên_operation}.json`)*

*Ví dụ:* Với `service: "catalog"`, `operation: "products"`, tạo file `Nexus.Gateway/Seeds/catalog/products.json`:

```json
{
  "items": [
    {
      "id": "22222222-2222-2222-2222-000000000001",
      "name": "Bàn phím cơ không dây Nexus Pro",
      "price": 1250000,
      "status": "Active"
    },
    {
      "id": "22222222-2222-2222-2222-000000000002",
      "name": "Chuột Gaming Silent Nexus X",
      "price": 650000,
      "status": "Active"
    }
  ],
  "total": 2
}
```

---

### 👉 Bước 3: Đảm bảo Service đặt `Mode: "Mock"` và Test trên Swagger
1. Kiểm tra cấu hình trong `Nexus.Gateway/gateway.runtime.json`:
   ```json
   "catalog": {
     "Mode": "Mock",
     "Address": "http://localhost:8002",
     "TimeoutSeconds": 5
   }
   ```
2. Mở trình duyệt truy cập: `http://localhost:8000/swagger`.
3. Tìm đến endpoint vừa khai báo trong Controller $\rightarrow$ bấm **Try it out** $\rightarrow$ **Execute**.
4. Gateway sẽ trả về response bọc trong vỏ bọc chuẩn `ApiResponse<T>`:
   ```json
   {
     "success": true,
     "statusCode": 200,
     "message": "Products retrieved successfully.",
     "data": {
       "items": [
         {
           "id": "22222222-2222-2222-2222-000000000001",
           "name": "Bàn phím cơ không dây Nexus Pro",
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

Toàn bộ API qua Gateway bắt buộc phải tuân theo chuẩn định dạng response đồng nhất:

### Phản hồi thành công (`ApiResponse<T>`):
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Sentence case success message in 100% English.",
  "data": { ... },
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

---

## ⚡ 5. Các Mẹo Tiện Ích Dành Cho Developer

### 1. Hot-Reload dữ liệu Seed tức thì
- Gateway hỗ trợ tự động tải lại file trong `Seeds/`. Khi sửa nội dung file JSON, bạn chỉ cần gọi lại API là dữ liệu mới lập tức phản hồi mà không cần khởi động lại Gateway.

### 2. Bỏ qua xác thực để test nhanh giao diện (Bypass Auth Mode)
Khi cần test nhanh giao diện frontend mà chưa có tài khoản hay JWT Token:
1. Mở file `Nexus.Gateway/gateway.runtime.json`.
2. Đổi `"AuthMode": "Bypass"`.
3. Lưu file lại $\rightarrow$ Bạn có thể gọi các API yêu cầu đăng nhập (`[Authorize]`) mà không bị chặn lỗi 401/403 (chỉ áp dụng ở môi trường Development).

### 3. Chuyển đổi giữa Mock và Real cho từng Service
- Khi Backend service (ví dụ Catalog gRPC trên port 8002) đã hoàn thiện và khởi động:
  - Mở `gateway.runtime.json`, sửa `"catalog": { "Mode": "Real" }`.
  - Hoặc gọi API quản trị: `PUT /api/v1/gateway/modes/catalog` với body `{"mode": "Real"}`.
  - Gateway sẽ lập tức định tuyến request sang backend gRPC thật!

### 4. Ghi đè Mode cho riêng 1 Request (Header Override)
- Khi service đang ở `Mock`, bạn có thể test thử 1 request sang backend thật bằng cách truyền HTTP Header trong Postman/cURL:
  ```http
  X-Service-Mode-catalog: Real
  ```
