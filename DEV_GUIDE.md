# Hướng dẫn phát triển API Gateway

Gateway .NET 8 có **10 controller** (`Nexus.Gateway/Controllers/`) cho HTTP API và cấu hình **8 backend service**: `users`, `catalog`, `commerce`, `log`, `auction`, `fulfillment`, `notification`, `wallet`. `GatewayControlController` quản lý mode và mock engine, không phải backend thứ chín. 

Toàn bộ hạ tầng phức tạp (Routing, Service Detection, Mode Evaluation, Security/Auth Bypass, Distributed Rate Limiting, Centralized Exception Handling) đã được dựng sẵn một lần duy nhất tại Gateway Pipeline. Khi phát triển API mới, developer chỉ cần viết controller siêu tinh gọn theo hướng dẫn dưới đây. Xem [README.md](README.md) để bắt đầu chạy.

---

## 1. Cơ chế tiền xử lý tự động khi request vào Gateway

Khi một request đi vào Gateway, hệ thống tự động xử lý các bước sau trước khi vào Action của Controller:

1. **URL Rewrite & Route Resolution**:
   - Tự động chuẩn hóa `/gateway/api/v1/...` về `/api/v1/...` để đảm bảo tương thích ngược.
   - Nhận diện Service phụ trách thông qua metadata `[BackendTag("service_name")]` trên Controller (hoặc suy từ URL).
2. **Endpoint & Mode Inspector**:
   - `GatewayRequestResolution` xác định ngay lập tức trạng thái của Service (`Mock` hay `Real`) và AuthMode (`Standard` hay `Bypass`).
   - Nếu ở chế độ `Mock` và có declarative mock definition trong `Mocks/*.mock.json`: Pipeline tự động trả kết quả giả lập chuẩn bọc trong `ApiResponse<T>`, không cần đi vào Controller.
   - Nếu ở chế độ `Real` (hoặc Controller cần logic động): Request tiếp tục đi qua các filter kiểm tra bảo mật, phân quyền RBAC và rate limit.
3. **Automatic Input Validation & Model Binding**:
   - DTO binding và DataAnnotations được ASP.NET Core kiểm tra tự động.
   - Mọi lỗi binding hoặc trường dữ liệu không hợp lệ tự động kích hoạt `InvalidModelStateResponseFactory` và trả về HTTP 400 ProblemDetails/ApiResponse chuẩn.
4. **Centralized Exception Handling**:
   - Developer **tuyệt đối không cần try-catch** trong Action và **không cần tự viết `if-else` để trả response `.Error(...)` thủ công**.
   - Khi có lỗi input, controller chỉ cần ném `GatewayInputException` hoặc gọi các helper: `pagination.EnsureValid()`, `ValidationHelpers.EnsureGuid()`, `MoneyPrecision.EnsurePositive()`.
   - Khi downstream backend gRPC trả lỗi (`RpcException`), `GatewayExceptionHandler` tự động map sang đúng HTTP status (400, 401, 403, 404, 409, 422, 503, 504...).

---

## 2. Thêm endpoint có backend: ví dụ `GET /api/v1/catalog/brands`

### Bước 1: Kiểm tra Contract Protobuf
Kiểm tra `Nexus.Gateway/Contracts/shop.proto` và backend Catalog. Thêm RPC/message **additive** nếu cần (không thay đổi tag field hiện có). Ví dụ:
```protobuf
rpc ListBrands(PageRequest) returns (BrandPage);
```
Khai báo `BrandPage` (`repeated Brand items = 1; int32 total = 2;`) và build để sinh gRPC client.

### Bước 2: Tạo Fixture Mock (cho Controller)
Tạo `Nexus.Gateway/Seeds/catalog/brands.json` chứa payload gRPC giả lập của `BrandPage`:
```json
{
  "items": [{ "id": "brand-01", "name": "Keychron", "logoUrl": "https://example.com/keychron.png" }],
  "total": 1
}
```

### Bước 3: Viết Action trong Controller (Siêu tinh gọn)
Trong `CatalogController.cs`, chỉ cần viết action ngắn gọn:

```csharp
[HttpGet("catalog/brands")]
[AllowAnonymous]
[ProducesResponseType(typeof(ApiResponse<PagedData<Brand>>), StatusCodes.Status200OK)]
public Task<IResult> ListBrands([FromQuery] PaginationRequest pagination)
{
    // 1 dòng validate tự động: nếu page/pageSize sai, tự ném GatewayInputException -> HTTP 400
    pagination.EnsureValid();

    // Điều phối tự động: service ở Mock thì đọc Seeds/catalog/brands.json, Real thì gọi gRPC
    return _dispatcher.DispatchPagedAsync(
        HttpContext, "catalog", "brands", pagination.Page, pagination.PageSize,
        o => _client.ListBrandsAsync(new PageRequest { Page = pagination.Page, PageSize = pagination.PageSize }, o),
        BrandPage.Parser);
}
```

### Bước 4: Viết Action tạo mới / cập nhật dữ liệu (POST / PUT)
Với các action nhận DTO:

```csharp
[HttpPost("catalog/brands")]
[Authorize("admin")]
[ProducesResponseType(typeof(ApiResponse<Brand>), StatusCodes.Status201Created)]
public Task<IResult> CreateBrand([FromBody] CreateBrandRequestDto body)
{
    // Validate nhanh qua helper tập trung:
    ValidationHelpers.EnsureRequired(body.Name, "Name");
    ValidationHelpers.EnsureGuid(body.CategoryId, "CategoryId");

    return _dispatcher.DispatchAsync(
        HttpContext, "catalog", "brand-create",
        o => _client.CreateBrandAsync(new BrandInput { Name = body.Name }, o),
        Brand.Parser,
        successMessage: "Brand created successfully.",
        successStatusCode: StatusCodes.Status201Created);
}
```

### Bước 5: Response chuẩn và Factory mẫu
Gateway hỗ trợ cả `IApiResponseFactory` tiêm qua DI và các static helper `ApiResponseFactory.Success` / `ApiResponseFactory.Error`:
```csharp
// Khi cần trả trực tiếp đối tượng ApiResponse chuẩn:
return Ok(ApiResponseFactory.Success(data, "Lấy dữ liệu thành công"));
```
Cấu trúc response thống nhất:
- **Thành công**: `{"success": true, "statusCode": 200, "message": "...", "data": {...}, "traceId": "...", "timestamp": "..."}`
- **Thất bại**: `{"type": "...", "title": "...", "status": 400, "errorCode": "VAL_INVALID_INPUT", "detail": "...", "errors": {...}, "traceId": "...", "timestamp": "..."}`

---

## 3. Xác minh & Kiểm thử

Chạy kiểm thử toàn bộ solution:
```bash
dotnet test ApiGateway.sln -c Release
dotnet format ApiGateway.sln --verify-no-changes --no-restore
```
Khi chạy local:
```bash
./run-dev.sh # Unix; PowerShell dùng ./run-dev.ps1
```
Swagger UI sẵn sàng tại `http://localhost:8000/swagger` (ở môi trường Development).
---

## 4. Hướng Dẫn Giám Sát Distributed Tracing (Grafana Tempo)

Gateway và toàn bộ hệ sinh thái Microservices được tích hợp sẵn hệ thống Distributed Tracing theo chuẩn OpenTelemetry & Grafana Tempo.

### Khởi Động Hạ Tầng Tracing
Chạy lệnh Docker Compose từ thư mục `src/Services/ApiGateway`:
```bash
docker compose up -d
```
Lệnh này khởi động 4 dịch vụ:
- `nexus-gateway-redis`: Cache và Distributed Rate Limiting (port 6379).
- `nexus-tempo`: OTLP Trace Backend (port 4317 gRPC, 4318 HTTP, 3200 API).
- `nexus-grafana`: Giao diện trực quan hóa Dashboard & Flame Graph (port 3000).
- `nexus-apigateway`: Ứng dụng API Gateway (port 8000).

### Tra Cứu Luồng Request Trên Grafana
1. Mở trình duyệt truy cập: `http://localhost:3000/explore`.
2. Datasource mặc định đã được cấu hình sẵn là **Tempo**.
3. Lấy mã `traceId` từ HTTP response header `X-Trace-Id` hoặc trường `traceId` trong JSON response của Gateway, dán vào ô **TraceQL** hoặc ô tìm kiếm.
4. Bạn sẽ thấy biểu đồ cây vết (Flame Graph / Waterfall):
   - Thời gian Gateway tiếp nhận request.
   - Thời gian gọi gRPC sang backend microservice.
   - Chi tiết thời gian thực thi câu lệnh SQL / Redis / RabbitMQ bên trong backend microservice.

---

## 5. Quy Chuẩn Health Prober & Logging Tập Trung
Xem tài liệu kiến trúc chuẩn tại [docs/HEALTH_AND_LOGGING_ARCHITECTURE.md](../../../docs/HEALTH_AND_LOGGING_ARCHITECTURE.md):
- **Health Prober Daemon:** Quản lý vòng đời sức khỏe 8 services (Interval 60s, Timeout 30s, Retries 5, StartPeriod 60s).
- **Incident File Logging:** Ghi log Unhealthy vào thư mục `logs/health/` với cơ chế giới hạn `MaxLinesPerLogFile = 1000` dòng/file.
- **Log Tập Trung Tương Lai (LogService):** Luôn áp dụng Batching $\ge 1000$ dòng trước khi gửi bulk sang LogService (Port 8004).
