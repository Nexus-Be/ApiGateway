# Hướng dẫn phát triển API Gateway
> **Trạng thái triển khai (2026-10-24):** ⚠️ Một phần — 15 file controller nhưng chỉ 4 file có action (16 action), 1/8 gRPC client (`users`), `shop.proto` chỉ có `UsersApi` 5 rpc.
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-24.
Gateway .NET 8 có **15 file controller** (`Nexus.Gateway/Controllers/`) cho HTTP API và cấu hình **8 backend service**: `users`, `catalog`, `commerce`, `log`, `auction`, `fulfillment`, `notification`, `wallet`. **Chỉ 4 controller đang có action thật** (`AuthController` 4 action, `UsersController` 1, `GatewayControlController` 6, `HealthProberController` 5 — tổng 16); 11 controller còn lại là stub 10 dòng chứa `// TODO (Team): ...` và **0 action**. `GatewayControlController` quản lý mode và mock engine, không phải backend thứ chín.
Toàn bộ hạ tầng phức tạp (Routing, Service Detection, Mode Evaluation, Security/Auth Bypass, Distributed Rate Limiting, Centralized Exception Handling) đã được dựng sẵn một lần duy nhất tại Gateway Pipeline. Khi phát triển API mới, developer chỉ cần viết controller siêu tinh gọn theo hướng dẫn dưới đây. Xem [README.md](README.md) để bắt đầu chạy.
---
## 1. Cơ chế tiền xử lý tự động khi request vào Gateway
Khi một request đi vào Gateway, hệ thống tự động xử lý các bước sau trước khi vào Action của Controller:
1. **URL Rewrite & Route Resolution**:
   - Tự động chuẩn hóa `/gateway/api/v1/...` về `/api/v1/...` để đảm bảo tương thích ngược (`Extensions/PipelineExtensions.cs:65-72`).
   - Nhận diện Service phụ trách thông qua metadata `[BackendTag("service_name")]` trên Controller (hoặc suy từ URL).
2. **Endpoint & Mode Inspector**:
   - `GatewayRequestResolution` xác định ngay lập tức trạng thái của Service (`Mock` hay `Real`) và auth mode (`Enforce`/`Standard` hay `Bypass`).
   - Nếu ở chế độ `Mock` **và route chưa có Controller Action khớp** nhưng có declarative mock definition trong `Mocks/*.mock.json`: `MockEngineMiddleware` tự động trả kết quả giả lập (`MockEngine/MockEngineMiddleware.cs:30-51`), không đi vào Controller.
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
> ⚠️ **Ví dụ dưới đây là ĐẶC TẢ MỤC TIÊU, chưa chạy được ngay.** Code hiện tại **chưa có** `rpc ListBrands`, message `BrandPage`/`Brand` trong `Nexus.Gateway/Contracts/shop.proto`, **chưa có** `CatalogApi.CatalogApiClient` (`Extensions/GrpcClientsExtension.cs` chỉ đăng ký `users`), và `ProductsController` vẫn là stub 0 action. Muốn ví dụ này chạy, phải làm Bước 1 (proto + sinh client) và đăng ký client trước.
### Bước 1: Kiểm tra Contract Protobuf
Kiểm tra `Nexus.Gateway/Contracts/shop.proto` và backend Catalog. Thêm RPC/message **additive** nếu cần (không thay đổi tag field hiện có). Trạng thái hiện tại: ❌ CHƯA CÓ (file chỉ có `service UsersApi` với 5 rpc). Đặc tả mục tiêu:
```protobuf
service CatalogApi {
  rpc ListBrands(PageRequest) returns (BrandPage);
}
message BrandPage { repeated Brand items = 1; repeated int32 total = 2; }
```
Sau khi thêm, đăng ký client trong `Extensions/GrpcClientsExtension.cs` (`services.AddNamedGrpcClient<CatalogApi.CatalogApiClient>("catalog")`) rồi build để sinh typed client.
### Bước 2: Tạo Fixture Mock (cho Controller)
Tạo `Nexus.Gateway/Seeds/catalog/brands.json` chứa payload gRPC giả lập của `BrandPage`:
```json
{
  "items": [{ "id": "brand-01", "name": "Keychron", "logoUrl": "https://example.com/keychron.png" }],
  "total": 1
}
```
Trạng thái: ❌ CHƯA CÓ — `Seeds/` hiện chỉ có `auth/` (4 file) và `users/` (5 file).
### Bước 3: Viết Action trong Controller (Siêu tinh gọn)
Trong `ProductsController.cs` (hoặc Controller tương ứng theo nghiệp vụ), chỉ cần viết action ngắn gọn. `ProductsController` hiện là stub route `api/v1/catalog/products` — cần thêm action thật:
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
Với các action nhận DTO (DTO phải được tạo trong `Dtos/Catalog/` — hiện chỉ có `Dtos/Auth`, `Dtos/Users`, `Dtos/GatewayControl`, `Dtos/HealthProber`):
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
return Ok(ApiResponseFactory.Success(data, "Dữ liệu đã được lấy thành công"));
```
Cấu trúc response thống nhất:
- **Thành công**: `{"success": true, "statusCode": 200, "message": "...", "data": {...}, "traceId": "...", "timestamp": "..."}`
- **Thất bại**: `{"type": "...", "title": "...", "status": 400, "errorCode": "VAL_INVALID_INPUT", "detail": "...", "errors": {...}, "traceId": "...", "timestamp": "..."}`
- Mọi chuỗi `message`/`detail` trả ra client **phải là tiếng Anh, sentence case** (Nguyên tắc 5 trong [AGENTS.md](AGENTS.md)).
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
Gateway có cấu hình OpenTelemetry (`Nexus.Gateway/Extensions/TelemetryExtension.cs`, `AddSource(BackendDispatcher.ActivityName)`), exporter OTLP lấy từ `OTEL_EXPORTER_OTLP_ENDPOINT`. **Tempo/Grafana không nằm trong repo này**: `docker-compose.yml` của ApiGateway chỉ định nghĩa duy nhất service `gateway` (port `${GATEWAY_PORT:-8000}`, mount `/var/run/docker.sock`, join network `nexus-network`). Hạ tầng tracing phải do cụm root cung cấp; nếu chưa có, biến `OTEL_EXPORTER_OTLP_ENDPOINT` trỏ tới endpoint không tồn tại và trace sẽ không được gửi đi.
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