# Hướng dẫn phát triển API Gateway
> **Trạng thái triển khai (2026-10-27):** ✅ Hoàn tất — 15/15 Controllers đã hoàn thiện đầy đủ action với 46 seed JSON fixtures trong `Seeds/`, 372/372 test cases pass 100%. Đã chuyển đổi hoàn toàn cơ chế giao tiếp downstream từ gRPC sang RESTful HTTP/JSON qua `HttpBackendCaller` và helper method tập trung `GatewayBaseController.ForwardAsync`.
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-27.

Gateway .NET 8 có **15 controllers** (`Nexus.Gateway/Controllers/`) cho HTTP API công khai và giao tiếp downstream RESTful tới **8 microservices backend**: `users` (8001), `catalog` (8002), `commerce` (8003), `log` (8004), `auction` (8005), `fulfillment` (8006), `notification` (8007), `wallet` (8008).

Toàn bộ hạ tầng phức tạp (Routing, Service Detection, Mode Evaluation Mock/Real, Security/Auth Bypass, Distributed Rate Limiting, Centralized Exception Handling, HTTP Downstream Forwarding) đã được dựng sẵn một lần duy nhất tại Gateway Pipeline và `GatewayBaseController`. Khi phát triển API mới, developer chỉ cần viết controller siêu tinh gọn kế thừa `GatewayBaseController` theo hướng dẫn dưới đây. Xem [README.md](README.md) để bắt đầu chạy.

---

## 1. Cơ chế tiền xử lý tự động khi request vào Gateway
Khi một request đi vào Gateway, hệ thống tự động xử lý các bước sau trước khi vào Action của Controller:
1. **URL Rewrite & Route Resolution**:
   - Tự động chuẩn hóa `/gateway/api/v1/...` về `/api/v1/...` để đảm bảo tương thích ngược (`Extensions/PipelineExtensions.cs:65-72`).
   - Nhận diện Service phụ trách thông qua metadata `[BackendTag("service_name")]` trên Controller (hoặc suy từ URL).
2. **Endpoint & Mode Inspector**:
   - `GatewayRequestResolution` xác định ngay lập tức trạng thái của Service (`Mock` hay `Real`) và auth mode (`Enforce`/`Standard` hay `Bypass`).
   - Nếu ở chế độ `Mock`: Controller tự động nạp dữ liệu từ file fixture JSON trong `Seeds/{service}/{mockSeed}.json`.
   - Nếu ở chế độ `Real`: Controller tự động chuyển tiếp request bằng HTTP call (GET/POST/PUT/DELETE) tới backend microservice qua `HttpBackendCaller` và `IHttpClientFactory`.
3. **Automatic Input Validation & Model Binding**:
   - DTO binding và DataAnnotations được ASP.NET Core kiểm tra tự động.
   - Mọi lỗi binding hoặc trường dữ liệu không hợp lệ tự động kích hoạt `InvalidModelStateResponseFactory` và trả về HTTP 400 ProblemDetails/ApiResponse chuẩn.
4. **Centralized Exception Handling**:
   - Developer **tuyệt đối không cần try-catch** trong Action và **không cần tự viết `if-else` để trả response `.Error(...)` thủ công**.
   - Khi có lỗi input, controller chỉ cần ném `GatewayInputException` hoặc gọi các helper: `pagination.EnsureValid()`, `ValidationHelpers.EnsureGuid()`, `MoneyPrecision.EnsurePositive()`.
   - Khi downstream backend trả lỗi HTTP hoặc unhandled exception, `GatewayExceptionHandler` tự động map sang đúng HTTP status (400, 401, 403, 404, 409, 422, 503, 504...).

---

## 2. Thêm API mới vào Gateway: Ví dụ `GET /api/v1/catalog/brands` và `POST /api/v1/catalog/brands`

Khi cần thêm một API mới trên Gateway, dev chỉ cần thực hiện 3 bước đơn giản:

### Bước 1: Khai báo DTOs
Tạo các DTOs tương ứng trong thư mục `Dtos/{ServiceName}/` (ví dụ `Dtos/Catalog/`):
```csharp
namespace Nexus.Gateway.Dtos.Catalog;

public record BrandResponse(string Id, string Name, string? LogoUrl);
public record CreateBrandRequest(string Name, string? LogoUrl);
```

### Bước 2: Tạo Fixture Mock (cho chế độ Mock ban đầu)
Tạo file fixture `Seeds/catalog/brands.json` (cho danh sách) hoặc `Seeds/catalog/create-brand.json`:
```json
{
  "items": [
    { "id": "brand-01", "name": "Keychron", "logoUrl": "https://example.com/keychron.png" },
    { "id": "brand-02", "name": "Logitech", "logoUrl": "https://example.com/logitech.png" }
  ],
  "total": 2
}
```

### Bước 3: Viết Action trong Controller (Kế thừa `GatewayBaseController`)
Trong Controller tương ứng (ví dụ `ProductsController.cs`):
```csharp
[ApiController]
[Route("api/v1/catalog")]
[BackendTag("catalog")]
public class ProductsController : GatewayBaseController
{
    public ProductsController(IGatewayDispatcher dispatcher) : base(dispatcher)
    {
    }

    // 1. API GET Phân Trang (Paged List)
    [HttpGet("brands")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PagedData<BrandResponse>>), StatusCodes.Status200OK)]
    public Task<IResult> ListBrands([FromQuery] PaginationRequest pagination)
    {
        pagination.EnsureValid();

        return ForwardPagedAsync<BrandResponse>(new()
        {
            Service = "catalog",
            DownstreamPath = "/api/v1/brands",
            Method = HttpMethod.Get,
            Page = pagination.Page,
            PageSize = pagination.PageSize,
            MockSeed = "brands",
            SuccessMessage = "Brands retrieved successfully."
        });
    }

    // 2. API POST Tạo Mới (Create)
    [HttpPost("brands")]
    [Authorize(GatewayPolicies.Admin)]
    [ProducesResponseType(typeof(ApiResponse<BrandResponse>), StatusCodes.Status201Created)]
    public Task<IResult> CreateBrand([FromBody] CreateBrandRequest body)
    {
        ValidationHelpers.EnsureRequired(body.Name, "Name");

        return ForwardAsync<CreateBrandRequest, BrandResponse>(new()
        {
            Service = "catalog",
            DownstreamPath = "/api/v1/brands",
            Method = HttpMethod.Post,
            Body = body,
            MockSeed = "create-brand",
            SuccessMessage = "Brand created successfully.",
            SuccessStatusCode = StatusCodes.Status201Created
        });
    }
}
```

### Bước 4: Phương thức công cụ `ForwardAsync` và `ForwardPagedAsync` làm gì bên dưới?
1. **Kiểm tra trạng thái Service**: Đọc cấu hình `Gateway:Services:catalog:Mode` trong `appsettings.json` (hoặc header `X-Gateway-Mode`).
2. **Nếu là `Mock`**: Tự động load file `Seeds/catalog/{MockSeed}.json`, deserialize thành DTO và bọc chuẩn `ApiResponse<T>` / `ApiResponse<PagedData<T>>`.
3. **Nếu là `Real`**:
   - Sử dụng `IHttpClientFactory` với Named Client `"catalog"`.
   - Tự động gắn các header ngữ cảnh quan trọng: `x-trace-id`, `x-user-id`, `x-user-role`, `Authorization`, `x-client-ip`.
   - Gửi HTTP request đến địa chỉ backend (`Gateway:Services:catalog:Address` - ví dụ `http://localhost:8002/api/v1/brands`).
   - Nhận JSON response từ backend, bọc hoặc passthrough chuẩn `ApiResponse<T>` trả về cho Frontend.
   - Nếu backend trả mã lỗi (4xx, 5xx) hoặc gặp lỗi mạng, tự động ném `GatewayDownstreamException` để middleware chuẩn hóa RFC 9110 ProblemDetails.

---

## 3. Cấu trúc Response Chuẩn
Toàn bộ phản hồi từ Gateway đều tuân theo chuẩn bất biến:
- **Thành công (200, 201)**:
  ```json
  {
    "success": true,
    "statusCode": 200,
    "message": "Brands retrieved successfully.",
    "data": { ... },
    "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
    "timestamp": "2026-10-27T08:00:00Z"
  }
  ```
- **Thất bại (400, 401, 403, 404, 500, 502)**:
  ```json
  {
    "type": "https://httpstatuses.io/400",
    "title": "Bad Request",
    "status": 400,
    "errorCode": "VAL_INVALID_INPUT",
    "detail": "Email is already registered in the system.",
    "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
    "timestamp": "2026-10-27T08:00:00Z"
  }
  ```
  *(100% Error message bằng tiếng Anh Sentence case, ErrorCode bằng `SCREAMING_SNAKE_CASE` theo Nguyên tắc 5 của `AGENTS.md`)*.

---

## 4. Xác minh & Kiểm thử
Chạy kiểm thử toàn bộ solution:
```bash
dotnet build ApiGateway.sln -c Release
dotnet test ApiGateway.sln -c Release
```
Khi chạy local:
```bash
./run-dev.sh # Unix; PowerShell dùng ./run-dev.ps1
```
```
Swagger UI sẵn sàng tại `http://localhost:8080/swagger` (ở môi trường Development).
---
## 4. Hướng Dẫn Giám Sát Distributed Tracing (Grafana Tempo)
Gateway có cấu hình OpenTelemetry (`Nexus.Gateway/Extensions/TelemetryExtension.cs`, `AddSource(BackendDispatcher.ActivityName)`), exporter OTLP lấy từ `OTEL_EXPORTER_OTLP_ENDPOINT`. **Tempo/Grafana không nằm trong repo này**: `docker-compose.yml` của ApiGateway chỉ định nghĩa duy nhất service `gateway` (port `${GATEWAY_PORT:-8080}`, mount `/var/run/docker.sock`, join network `nexus-network`). Hạ tầng tracing phải do cụm root cung cấp; nếu chưa có, biến `OTEL_EXPORTER_OTLP_ENDPOINT` trỏ tới endpoint không tồn tại và trace sẽ không được gửi đi.
### Khởi Động Gateway
```bash
docker compose up -d          # chỉ dựng nexus-apigateway từ Dockerfile trong repo này
```
### Tra Cứu Luồng Request Trên Grafana
1. Mở trình duyệt truy cập Grafana của cụm hạ tầng (mặc định tài liệu cũ ghi `http://localhost:3000/explore` — chỉ đúng nếu cụm đó map port 3000).
2. Chọn datasource **Tempo** (không tự động cấu hình sẵn bởi repo này).
3. Lấy mã `traceId` từ HTTP response header `X-Trace-Id` hoặc trường `traceId` trong JSON response của Gateway, dán vào ô **TraceQL** hoặc ô tìm kiếm.
4. Bạn sẽ thấy biểu đồ cây vết (Flame Graph / Waterfall):
   - Thời gian Gateway tiếp nhận request.
   - Thời gian gọi gRPC sang backend microservice (hiện chỉ service `users` có client).
---
## 5. Quy Chuẩn Health Prober & Logging Tập Trung
Xem tài liệu kiến trúc tại [specs/01_ARCHITECTURAL_BLUEPRINT.md](specs/01_ARCHITECTURAL_BLUEPRINT.md):
- **Health Prober Daemon** ✅: Quản lý vòng đời sức khỏe 8 services (Interval 60s, Timeout 30s, Retries 5, StartPeriod 60s), mặc định `Disabled` cho tới khi bật qua API.
- **Incident File Logging** ✅: Ghi log Unhealthy vào `logs/health/` với `MaxLinesPerLogFile = 1000` dòng/file.
- **Saga Rollback** ❌: `HealthProber/SagaRollbackPublisher.cs` **chỉ ghi log**, chưa publish lên RabbitMQ/MassTransit — đặc tả mục tiêu, chưa triển khai.
- **Docker Auto-Recovery** ⚠️: `HealthProber/DockerContainerRecoveryService.cs:26-55` gọi CLI `docker restart`; image runtime `mcr.microsoft.com/dotnet/aspnet:8.0` không có docker CLI nên trong container luôn thất bại (chỉ chạy được trên host có docker CLI).
- **Log Tập Trung (LogService)** ⚠️: `Logging/GatewayBatchLogSink.cs:56-63` POST tới `{Serilog:Batching:RemoteEndpoint}/api/v1/log/batch` (mặc định `http://localhost:8004`) nhưng **endpoint này không tồn tại** ở Log service (`Log.Api/Program.cs` chỉ map `/health` và `/`), response bị bỏ qua và lỗi bị `catch {}` nuốt im lặng.