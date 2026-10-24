# 01 — KIẾN TRÚC 3 KHỐI ĐỘC LẬP (3-TIER DECOUPLING BLUEPRINT)
> **Trạng thái triển khai (2026-10-24):** ⚠️ Một phần — tầng Dispatching/Output đã có đủ lớp; tầng Input mới có 4/15 controller chứa action (16 action), 11 controller là stub.
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-24.
Gateway được tái cấu trúc thành 3 khối phân tách triệt để, đảm bảo trách nhiệm đơn nhất (Single Responsibility Principle) và dễ dàng bảo trì mở rộng.
---
## 1. SƠ ĐỒ ĐIỀU PHỐI 3 TẦNG
```
[ HTTP Client Request ]
         │
         ▼
1. TẦNG ĐẦU VÀO: 15 file Controller (Controllers/) — chỉ 4 file có action thật
   - Nhận DTOs (trong Dtos/{Service}/*Requests.cs; hiện có Auth, Users, GatewayControl, HealthProber).
   - Kiểm tra input qua ValidationHelpers (Bad, PageOk, GuidOk).
   - Gọi 1 dòng: _dispatcher.DispatchAsync(...) hoặc _dispatcher.DispatchPagedAsync(...).
   - Bao gồm HealthProberController quản lý daemon giám sát sức khỏe & test thủ công.
         │
         ▼
2. TẦNG PHẦN GIỮA: The Dispatching Core (Dispatching/)
   - ModeEvaluator: Phân giải service và effective mode (Real / Mock).
   - MockDataMapper: Nạp file JSON từ Seeds/{service}/{entity}.json, thẩm định qua MessageParser<T>.ParseJson().
   - GrpcServiceCaller: Đính kèm Authorization, Idempotency-Key, x-trace-id và gọi typed gRPC Client qua HTTP/2.
   - GatewayDispatcher: Nhận lệnh -> phối hợp 3 bộ trên -> chuyển dữ liệu gốc sang Đầu Ra.
         │
         ▼
3. TẦNG ĐẦU RA: Universal Output & Exception Handling (Responses/ & Middleware/)
   - ApiResponseFactory: Không quan tâm data từ Mock hay Real, tự động bọc ApiResponse<T> hoặc ApiResponse<PagedData<T>>, gắn traceId và ISO timestamp.
   - GatewayExceptionHandler: Bắt mọi lỗi RpcException, Timeout, Validation map sang ApiErrorResponse RFC 9110 / RFC 6585.
         │
         ▼
[ HTTP JSON Response ]
```
---
## 2. BẢN ĐỒ CÁC LỚP TRONG DISPATCHING CORE
| Lớp | Trạng thái | Bằng chứng |
|:---|:---|:---|
| `IModeEvaluator` / `ModeEvaluator` | ✅ CÓ | `Nexus.Gateway/Dispatching/ModeEvaluator.cs:8-14` |
| `IMockDataMapper` / `MockDataMapper` | ✅ CÓ | `Nexus.Gateway/Dispatching/MockDataMapper.cs:27` |
| `IGrpcServiceCaller` / `GrpcServiceCaller` | ✅ CÓ | `Nexus.Gateway/Dispatching/GrpcServiceCaller.cs:24` |
| `IGatewayDispatcher` / `GatewayDispatcher` (Circuit Breaker Fast-Fail) | ✅ CÓ | `Nexus.Gateway/Dispatching/GatewayDispatcher.cs:33` |
| `BackendDispatcher` (wrapper tương thích ngược) | ✅ CÓ | `Nexus.Gateway/BackendDispatcher.cs:8` |
1. **`IModeEvaluator` / `ModeEvaluator`** (`Dispatching/ModeEvaluator.cs`):
   - Truy vấn cấu hình từ `IOptionsMonitor<GatewayOptions>`.
   - Kết hợp với `IGatewayRuntimeManager` để hỗ trợ Hot-Reload từ `gateway.runtime.json` và Header Override (`X-Service-Mode-{service}: Real`).
2. **`IMockDataMapper` / `MockDataMapper`** (`Dispatching/MockDataMapper.cs`):
   - Quét tìm file seed JSON: Ưu tiên `Seeds/{service}/{operation}.json`, dự phòng `Mocks/{service}/{operation}.json`.
   - Áp dụng `MockDelayMs` và `MockStatus` nếu được cấu hình.
   - Parse JSON qua `MessageParser<T>.ParseJson()` $\rightarrow$ Nếu file seed JSON sai kiểu dữ liệu hoặc thiếu trường so với Protobuf, lỗi sẽ bị bắt ngay lập tức.
   - ⚠️ MỘT PHẦN: hiện chỉ có seed thật cho `users` và `auth` (9 file); các service khác chưa có seed nên endpoint Mock tương ứng chưa trả được dữ liệu.
3. **`IGrpcServiceCaller` / `GrpcServiceCaller`** (`Dispatching/GrpcServiceCaller.cs`):
   - Tạo `CallOptions` với `deadline` dựa trên `TimeoutSeconds`.
   - Forward `Authorization: Bearer <token>`, `Idempotency-Key`, và `x-trace-id`.
   - Thực thi cuộc gọi gRPC bất đồng bộ.
   - ⚠️ MỘT PHẦN: chỉ service `users` có typed client được DI (`Extensions/GrpcClientsExtension.cs`), nên chỉ service này gọi được Real.
4. **`IGatewayDispatcher` / `GatewayDispatcher`** (`Dispatching/GatewayDispatcher.cs`):
   - Tích hợp **Circuit Breaker Fast-Fail** cùng `IHealthProberManager`: Nếu service con bị Unhealthy hoặc đang Recovering, lập tức ngắt mạch trong 0ms trả về HTTP 503 `DOWNSTREAM_UNHEALTHY`.
   - Cung cấp hai phương thức điều phối duy nhất:
     - `Task<IResult> DispatchAsync<T>(...)`: Cho các endpoint trả về đối tượng đơn lẻ.
     - `Task<IResult> DispatchPagedAsync<T>(...)`: Cho các endpoint trả về danh sách phân trang có `pageIndex`, `pageSize`, `totalCount`.
5. **`BackendDispatcher`** (`Nexus.Gateway/BackendDispatcher.cs`):
   - Cầu nối tương thích ngược (compatibility wrapper) ủy quyền trực tiếp sang `IGatewayDispatcher`; đăng ký singleton tại `Extensions/CoreServicesExtension.cs:73`.
---
## 3. PIPELINE TRUNG GIAN THỰC TẾ (`Extensions/PipelineExtensions.cs`)
Thứ tự middleware đang chạy (đọc trực tiếp từ `UseGatewayPipeline`):
| Bước | Hành vi | Trạng thái | Bằng chứng |
|:---:|:---|:---|:---|
| 1 | `UseExceptionHandler` + `UseStatusCodePages` + `UseSerilogRequestLogging` (enrich `TraceId`, `BackendTag`, `GatewayMode`) | ✅ CÓ | `Extensions/PipelineExtensions.cs:22-40` |
| 2 | Header `X-Trace-Id`, `X-Content-Type-Options`, `X-Gateway-Mode`, `Cache-Control: no-store` khi Mock | ✅ CÓ | `Extensions/PipelineExtensions.cs:43-62` |
| 3 | Rewrite `/gateway/api/*` → `/api/*` | ✅ CÓ | `Extensions/PipelineExtensions.cs:65-72` |
| 4 | `UseRouting` → `UseCors` → `GatewayRequestResolution.Resolve` (snapshot per-request) | ✅ CÓ | `Extensions/PipelineExtensions.cs:74-85` |
| 5 | `UseMockSecurityAdapter` → `UseAuthentication` → `UseRedisRateLimit` → `UseRateLimiter` → `UseAuthorization` | ✅ CÓ | `Extensions/PipelineExtensions.cs:86-90` |
| 6 | `UseMockEngine` (declarative mock) → `UseRequestTimeouts` (65s, 504) | ✅ CÓ | `Extensions/PipelineExtensions.cs:91-92`; `Extensions/CoreServicesExtension.cs:93-97` |
| 7 | Health endpoint: `/health/live` (`MapHealthChecks`), `/health/ready` (503 khi options không hợp lệ) | ✅ CÓ | `Extensions/PipelineExtensions.cs:101-103` |
| 8 | `/` → redirect `/swagger` (chỉ Development) + Swagger UI | ✅ CÓ | `Extensions/PipelineExtensions.cs:94-99` |