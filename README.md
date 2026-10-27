# Nexus API Gateway (.NET 8)
> **Trạng thái triển khai (2026-10-24):** ⚠️ Một phần — 4/15 controller có action (16 action thật), 1/8 gRPC client, 1 service trong `shop.proto` (5 rpc), 62 attribute test trên 19 file.
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-24.
HTTP gateway gồm **15 file controller** trong `Nexus.Gateway/Controllers/` (tổng 555 dòng) và cấu hình **8 backend service**: `users`, `catalog`, `commerce`, `log`, `auction`, `fulfillment`, `notification`, `wallet`. Trong 15 controller đó **chỉ 4 controller có action thật** (16 action: 5 `GET`, 6 `POST`, 5 `PUT`); **11 controller còn lại là stub 10 dòng, 0 action**, chỉ chứa `// TODO (Team): ...` — xem bảng bên dưới. Controller dispatch qua gRPC khi service ở `Real`, hoặc đọc fixture protobuf JSON trong `Nexus.Gateway/Seeds/` khi ở `Mock`. `Nexus.Gateway/Mocks/*.mock.json` là **declarative mock routes** riêng (6 file, phục vụ Mock cho route chưa có Controller Action), không tự tạo RPC hay route Real. Xem [DEV_GUIDE.md](DEV_GUIDE.md) để thêm API và [MOCK_GUIDE.md](MOCK_GUIDE.md) để biết cách tạo dữ liệu mock.
## Trạng thái thực tế theo thành phần
| Thành phần | Trạng thái | Bằng chứng |
|:---|:---|:---|
| `AuthController` — 4 action (`register`, `verify-otp`, `resend-otp`, `login`) | ✅ CÓ | `Nexus.Gateway/Controllers/AuthController.cs:14-107` |
| `UsersController` — 1 action (`GET /api/v1/users/me`) | ✅ CÓ | `Nexus.Gateway/Controllers/UsersController.cs:27-28` |
| `GatewayControlController` — 6 action mode/mock-engine | ✅ CÓ | `Nexus.Gateway/Controllers/GatewayControlController.cs:34-121` |
| `HealthProberController` — 5 action prober | ✅ CÓ | `Nexus.Gateway/Controllers/HealthProberController.cs:33-124` |
| 11 controller nghiệp vụ (AdminUsers, Cart, Orders, Products, Categories, Inventory, Auction, Fulfillment, Notification, Log, Wallet) | ❌ CHƯA CÓ | Mỗi file 10 dòng, 0 `[Http*]`, body chỉ có `// TODO (Team)` — đặc tả mục tiêu, chưa triển khai |
| gRPC typed client | ⚠️ MỘT PHẦN | `Nexus.Gateway/Extensions/GrpcClientsExtension.cs:` chỉ đăng ký `UsersApi.UsersApiClient` ("users"); 7/8 service chưa có client |
| Hợp đồng Protobuf | ⚠️ MỘT PHẦN | `Nexus.Gateway/Contracts/shop.proto:` chỉ có `service UsersApi` với 5 rpc (`Register`, `Login`, `VerifyOtp`, `ResendOtp`, `GetProfile`) |
| Seed fixture | ⚠️ MỘT PHẦN | Chỉ có `Seeds/auth/*.json` (4) và `Seeds/users/*.json` (5); các thư mục `Seeds/cart|orders|products/` mới chỉ có `README.md` |
| Declarative mock engine | ✅ CÓ | `Nexus.Gateway/Mocks/*.mock.json` (6 file) + `Nexus.Gateway/MockEngine/MockEngineMiddleware.cs` |
| Health Prober + Circuit Breaker | ✅ CÓ | `Nexus.Gateway/HealthProber/HealthProberManager.cs`, `HealthProber/DockerContainerRecoveryService.cs` |
| Saga Rollback lên RabbitMQ | ❌ CHƯA CÓ | `Nexus.Gateway/HealthProber/SagaRollbackPublisher.cs:` chỉ ghi log, không có RabbitMQ/MassTransit; đặc tả mục tiêu, chưa triển khai |
| Docker auto-recovery | ⚠️ MỘT PHẦN | `DockerContainerRecoveryService.cs:26-55` gọi CLI `docker restart`; image runtime `mcr.microsoft.com/dotnet/aspnet:8.0` (xem `Dockerfile`) **không có docker CLI** → trong container luôn thất bại |
## Chạy và kiểm thử
Từ thư mục `src/Services/ApiGateway`:
```bash
dotnet test ApiGateway.sln -c Release
./run-dev.sh # Unix; PowerShell dùng ./run-dev.ps1
```
`run-dev.sh` chạy `dotnet run --project Nexus.Gateway --no-launch-profile --urls http://127.0.0.1:8080`, tức Gateway nghe ở cổng `8080`. `docker-compose.yml` trong repo này **chỉ định nghĩa duy nhất service `gateway`** (build từ `Dockerfile`, mount `/var/run/docker.sock`, port `${GATEWAY_PORT:-8080}`); Redis/Tempo/Grafana không nằm trong file compose này mà do cụm hạ tầng bên ngoài cung cấp qua `nexus-network` (biến `REDIS_CONNECTION_STRING`, `OTEL_EXPORTER_OTLP_ENDPOINT`). Swagger (`/swagger`) chỉ bật ở Development. Không giả định backend Real sẵn sàng: phải triển khai RPC tương ứng và cấu hình địa chỉ trước khi gọi.
## Giám sát sức khỏe & Tự phục hồi (Health Prober Daemon)
Gateway tích hợp sẵn **Health Prober Daemon Engine** giám sát định kỳ 8 backend services:
- **Chu kỳ & Ngưỡng**: `Interval: 60s` (1 phút), `Timeout: 30s`, `Retries: 5 lần`, `StartPeriod: 60s` ân hạn khởi động (khớp `Gateway:HealthProber` trong `Nexus.Gateway/appsettings.json`).
- **State Machine**: `Disabled` (mặc định) $\rightarrow$ `Starting` $\rightarrow$ `Healthy` $\leftrightarrow$ `Degraded` $\rightarrow$ `Unhealthy` $\rightarrow$ `Recovering` (`Nexus.Gateway/HealthProber/ProberRunStatus.cs`; khởi tạo mặc định `Disabled` tại `HealthProber/HealthProberManager.cs:53`).
- **Cơ chế phản ứng khi Unhealthy**:
  - **Circuit Breaker Fast-Fail**: Ngắt mạch tức thì trong 0ms tại `GatewayDispatcher`, trả về HTTP 503 `DOWNSTREAM_UNHEALTHY`.
  - **Saga Rollback**: ⚠️ MỘT PHẦN — `SagaRollbackPublisher` mới chỉ log sự kiện `ServiceUnhealthyIntegrationEvent`, chưa publish lên RabbitMQ.
  - **Docker Auto-Recovery**: ⚠️ MỘT PHẦN — gọi `docker restart nexus-{service}-service` qua CLI (tối đa 3 lần); image runtime không kèm docker CLI nên chỉ dùng được khi chạy trên host có docker CLI.
- **Log sự cố**: Lưu tại `logs/health/incident-YYYYMMDD-XXXX.log` với cơ chế xoay file tối đa `1000` dòng/file. Healthy không ghi file log.
- **API Quản trị (Admin)**: `GET /api/v1/gateway/prober/status`, `GET /api/v1/gateway/prober/status/{service}`, `PUT /api/v1/gateway/prober/{service}`, `PUT /api/v1/gateway/prober/bulk`, và `POST /api/v1/gateway/prober/{service}/check` (kiểm tra thủ công tức thì). Chi tiết xem [specs/01_ARCHITECTURAL_BLUEPRINT.md](specs/01_ARCHITECTURAL_BLUEPRINT.md).
## Service mode và auth mode là hai trục khác nhau
Trong `Nexus.Gateway/gateway.runtime.json` (file này **được track trong git**), đặt `Gateway:Services:<service>:Mode` thành `Mock` hoặc `Real`, và `Gateway:AuthMode` thành `Enforce` (mặc định) hoặc `Bypass`. Giá trị đang lưu trong repo: `users` và `auction` ở `Mock`, 6 service còn lại ở `Real`, `AuthMode: "Enforce"`, nhưng bật `AllowAnonymousMockAuthInDevelopment`, `AllowMockTokenAuth`, `RedisFailOpenInDevelopment` và gán `DefaultMockUserId` / `DefaultMockRole` — nghĩa là **Bypass chỉ bật khi được yêu cầu và chỉ có hiệu lực ở Development**, không phải mặc định của repo. Có thể cập nhật qua control API `/api/v1/gateway/modes/{service}` và `/api/v1/gateway/mode` với **quyền Gateway admin**; endpoint control có `[DisableMockAuthorizationBypass]`. Override theo request header chỉ hoạt động ở Development khi cấu hình cho phép. File cấu hình đăng ký reload khi thay đổi, nhưng nên xác minh mode có hiệu lực bằng request thay vì giả định hot reload tức thì.
`Enforce` giữ xác thực/phân quyền cho cả request Mock và Real. `Bypass` chỉ có hiệu lực trong **Development**, trên request Mock đủ điều kiện khi tùy chọn anonymous mock auth bật; không áp dụng vô điều kiện, không bỏ qua endpoint bị cấm bypass. Security và authorization được xử lý trước controller/mock response. Một declarative-only route khi service chuyển sang `Real` mà không có controller route tương ứng sẽ trả **404**, không tự forward sang backend.
Response controller thành công là envelope `success`, `statusCode`, `data`, `traceId`, `timestamp` (có thể có `message`), HTTP **200** theo mặc định; dispatcher hỗ trợ HTTP **201** khi endpoint khai báo rõ. Lỗi dùng shape `type`, `title`, `status`, `errorCode`, `detail`, `traceId`, `timestamp` (có thể có `errors`), HTTP status tương ứng; Exception Handler trung tâm tự động map lỗi và trả về đúng status code mà không cần controller try-catch hay gọi .Error(...) thủ công. Declarative `raw` cố ý trả body nguyên bản. Xem [DEV_GUIDE.md](DEV_GUIDE.md) để biết cách viết action siêu tinh gọn, phân trang, validation và phân biệt fixture/declarative mock.
## Cấu hình & bí mật
- `Nexus.Gateway/appsettings.json` và `appsettings.Development.json` bị `.gitignore` (`appsettings*.json`) — **không** được commit. Trong working tree hiện tại hai file này đang chứa `Jwt:Signature` hardcode; bản mẫu `appsettings.example.json` / `appsettings.Development.example.json` dùng placeholder `<REPLACE_WITH_JWT_SIGNING_KEY_MIN_32_CHARS>`. Không commit khóa thật; nạp qua biến môi trường `Jwt__Signature` khi triển khai.
- Thứ tự nạp: `appsettings.json` → `appsettings.{Environment}.json` → `gateway.runtime.json` → biến môi trường (`Nexus.Gateway/Extensions/ConfigurationExtensions.cs`). Chi tiết từng khóa xem [CONFIG_REFERENCE.md](CONFIG_REFERENCE.md).
## Kiểm thử
`Nexus.Gateway.Tests/` có **19 file .cs** (2012 dòng), **49 `[Fact]` + 13 `[Theory]` = 62 attribute test**; các `[Theory]` dùng tổng cộng 49 dòng `InlineData`. Không ghi con số "N/N passed" cố định trong tài liệu — chạy `dotnet test ApiGateway.sln -c Release` để lấy kết quả thật của lần chạy hiện tại.