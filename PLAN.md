# PLAN — Kế hoạch & Tiến trình Mở rộng Nexus API Gateway (Gateway Mode State)
> **Trạng thái triển khai (2026-10-24):** ⚠️ Một phần — đây là **nhật ký kế hoạch lịch sử**, không phải báo cáo nghiệm thu hiện tại. Số liệu thật: 4/15 controller có action (16 action), 11 stub rỗng; 1/8 gRPC client; `shop.proto` chỉ có `UsersApi` 5 rpc; 62 attribute test trên 19 file. Mọi con số "N/N tests pass" cũ trong tài liệu này **đã bị loại bỏ** vì không tái lập được từ code.
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-24.
Tài liệu này ghi nhận kế hoạch tổng thể, các kế hoạch con (sub-plans), tiêu chí nghiệm thu (checkpoints), bằng chứng kiểm thử từng phần và kết quả chạy thử nghiệm thực tế (live run E2E) bổ sung trạng thái Gateway Mode linh hoạt giữa kiểm tra đăng nhập bình thường và chế độ Mock độc lập.
> ⚠️ **Đọc kèm mục 0.5 "Đối chiếu với code hiện tại"** ở cuối tài liệu: một số mục checkbox `[x]` bên dưới đánh dấu *ý định đã hoàn thành tại thời điểm viết*, nhưng code hiện tại **không** còn đúng như vậy (đặc biệt Plan 4, Plan 8, Plan 9).
---
## 0. Đối chiếu Yêu cầu Nghiệp vụ
1. **Trạng thái Mặc định (`Gateway:AuthMode = "Enforce"` / `"Standard"`)**:
   - Vẫn cần đăng nhập và kiểm tra quyền mọi thứ như thường.
   - Chưa đăng nhập (không gửi token) ➔ Bị chặn với mã `401 Unauthorized`.
   - Đăng nhập (`POST /api/v1/auth/login`) lấy token ➔ Gửi token vào request để truy cập `200 OK`.
   - Sai vai trò/quyền (ví dụ buyer gọi endpoint admin) ➔ Bị chặn với mã `403 Forbidden`.
2. **Trạng thái Mock/Bypass (`Gateway:AuthMode = "Bypass"`)**:
   - Bật khi team muốn phát triển giao diện nhanh hoặc test độc lập.
   - **KHÔNG CHECK AUTH**: Không bắt buộc đăng nhập, tự động cấp quyền tương ứng cho các API.
   - **CÁC API ĐỘC LẬP**: Các API mock chạy hoàn toàn độc lập mà không bị ràng buộc vào login hay service user.
   - Dễ dàng bật/tắt qua file cấu hình `gateway.runtime.json` hoặc gọi API `PUT /api/v1/gateway/mode`.
> ⚠️ **Tên cấu hình thật**: thuộc tính là `GatewayOptions.AuthMode` (`Nexus.Gateway/GatewayOptions.cs:38`), giá trị hợp lệ `Enforce` (mặc định) | `Standard` | `Bypass` (kèm alias `Mock`, `MockBypass`). Tài liệu cũ gọi là `GatewayMode` — tên đó chỉ còn là **alias legacy** (`:42`). `Bypass`/`Mock`/`MockBypass` **chỉ hợp lệ khi `ASPNETCORE_ENVIRONMENT=Development`** (`:110-115`), production fail-fast khi khởi động.
---
## 1. Kế hoạch Tổng thể & Các Kế hoạch Con (Phases)
- **Plan 1: Bổ sung trạng thái AuthMode trong GatewayOptions, config và IGatewayRuntimeManager**
  - [x] Thêm thuộc tính `AuthMode` (`"Enforce"` | `"Standard"` | `"Bypass"`) vào `GatewayOptions` và `gateway.runtime.json`.
  - [x] `IGatewayRuntimeManager` (`Nexus.Gateway/Services/IGatewayRuntimeManager.cs:29`) cung cấp `GetAuthMode`, `SetAuthModeAsync`, `GetServiceModes`, `SetServiceModeAsync`, `SetBulkServiceModesAsync`, `SetBulkModesAsync`. *(Tài liệu cũ ghi `GetGatewayMode`/`SetGatewayModeAsync`/`IsAuthBypassed` — các tên này **không tồn tại** trong code.)*
  - *Checkpoint*: Build thành công.
- **Plan 2: Cập nhật Security theo 2 trạng thái**
  - [x] Khi `Enforce`/`Standard`: Giữ nguyên kiểm tra đăng nhập và phân quyền JWT/RBAC bình thường.
  - [x] Khi `Bypass`: Tự động nới lỏng auth và cấp quyền thích ứng để các API mock chạy độc lập (chỉ Development).
  - *Checkpoint*: Build thành công.
- **Plan 3: Nâng cấp API Gateway Modes**
  - [x] `GET /api/v1/gateway/modes`: Trả về cả `authMode` và danh sách `services` (`GatewayControlController.cs:34`).
  - [x] `PUT /api/v1/gateway/mode`: Cho phép bật/tắt runtime giữa `Enforce`/`Standard`/`Bypass` (`:42`, body `AuthModeUpdateRequestDto`).
  - [x] `PUT /api/v1/gateway/modes`: Hỗ trợ cập nhật đồng thời nhiều service và auth mode (`:74`); kèm `PUT /api/v1/gateway/modes/{service}` (`:58`).
  - *Checkpoint*: Build thành công.
- **Plan 4: Nâng cấp Mock Login sinh token theo tài khoản**
  - [x] *(ý định)* `POST /api/v1/auth/login` trả về token hợp lệ cho mock flow.
  - ⚠️ **Hiện trạng: KHÔNG đạt.** Action có thật (`AuthController.cs:106` → `_client.LoginAsync`) nhưng `rpc Login` của `UsersApi` **chưa được implement ở User service** (GW-03) và gateway chỉ có **một** gRPC client `users`; ở `Mode: Mock`, login đọc seed `Seeds/auth/login.json` nên không thực sự "sinh token theo tài khoản".
  - *Checkpoint*: Build thành công.
- **Plan 5: Viết Test Suite tự động kiểm chứng cả 2 trạng thái**
  - [ ] Bổ sung test cho 2 trạng thái: tên `Gateway_Standard_mode_strictly_requires_login_as_normal` và `Gateway_Bypass_mode_skips_auth_check_and_runs_independently` **không tồn tại** trong repo. Test thật gần nhất: `GatewayTests.Mock_does_not_bypass_jwt` (`Nexus.Gateway.Tests/GatewayTests.cs:61`), `GatewayRequestResolutionTests`, `BulkModesAtomicityTests.Invalid_bulk_update_leaves_auth_and_service_modes_unchanged`.
  - [ ] Chạy `dotnet test ApiGateway.sln -c Release`: **không ghi con số** trong tài liệu; phải lấy từ output thực tế. Quy mô test hiện tại: **62 attribute `[Fact]`/`[Theory]` trên 19 file** (49 `[Fact]` + 13 `[Theory]`, trong đó 49 dòng `[InlineData]`).
  - *Checkpoint*: nhật ký cũ ghi "46/46" và "40 tests" — **hai con số này mâu thuẫn nhau và không tái lập được**, đã bị xoá.
- **Plan 6: Chạy live server Gateway, test trực tiếp cả 2 luồng và nghiệm thu**
  - [x] Chạy live server Gateway thực tế cổng **8000** (`Properties/launchSettings.json`, `Dockerfile EXPOSE 8000`, `run-dev.sh`):
    - Luồng `Enforce`: Gọi không token ➔ 401; Đăng nhập lấy token ➔ 200 OK *(login phụ thuộc GW-03)*.
    - Luồng `Bypass`: Đổi mode sang `Bypass` ➔ gọi `GET /api/v1/users/me` không cần token ➔ 200 OK. Lưu ý: `/api/v1/catalog/inventory` (`api/v1/catalog/inventory`) và `/api/v1/admin/users` (`api/v1/admin/users`) là **stub 0 action** nên trả **404**, không trả 200 — tài liệu cũ ghi `/seller/inventory`, `/admin/roles` là **route không tồn tại**.
    - Reset về `Enforce`: Gọi lại không token ➔ Lập tức bị chặn 401 như thường.
  - *Ghi chú*: tài liệu cũ ghi cổng `5200` — sai với port bất biến `8000`.
- **Plan 7: Tái cấu trúc 3 Khối Độc Lập (3-Tier Decoupling: Input → Middle → Output)**
  - [x] **Tầng Common & Responses**: Chuẩn hóa `ApiResponseFactory`, `ValidationHelpers`, `HttpContextUserExtensions`, attributes.
  - [x] **Khối Phần Giữa (Dispatching Core)**: `ModeEvaluator`, `MockDataMapper`, `GrpcServiceCaller`, `GatewayDispatcher`, `BackendDispatcher`.
  - [x] **Dữ Liệu Seed (`Seeds/`)**: Tổ chức `Seeds/{service}/{entity}.json`, Gateway không giữ stateful DB in-memory, thẩm định contract qua Protobuf MessageParser. Hiện có 9 file seed (`auth` 4, `users` 5).
  - [x] **Tách biệt DTOs (`Dtos/`)**: Chuyển từ Models sang DTOs, tách `*Requests.cs` / `*Responses.cs`. ⚠️ Thực tế chỉ **4/8 domain** có DTO: `Auth`, `GatewayControl`, `HealthProber`, `Users` (tài liệu cũ ghi "toàn bộ 10 domains" — sai).
  - [x] **Khối Đầu Vào (Controllers)**: **15** controller `[ApiController] ControllerBase` trong `Controllers/` (tài liệu cũ ghi 10), loại bỏ hoàn toàn thư mục `Endpoints/` cũ. ⚠️ Chỉ **4** controller có action (**16 action**); **11** controller là stub đúng 10 dòng, 0 action.
  - [x] **Tích hợp DI & Route**: `services.AddControllers()` + `app.MapControllers()`, `Program.cs` **30 dòng** (đạt mốc "< 35 dòng").
  - [ ] **Kiểm thử tự động**: mốc "46/46 PASS" đã xoá — xem Plan 5 để có quy mô test thật.
  - [x] **Đồng bộ tài liệu**: Cập nhật `dac_ta.md`, `DEV_GUIDE.md`, `README.md`, `PLAN.md` (bản này).
- **Plan 8: Xây dựng Hệ Thống Health Prober Daemon, Giám Sát Sức Khỏe & Tự Hồi Phục (Self-Healing)**
  - [x] **State Machine**: `ProberRunStatus` với 6 trạng thái (`Disabled`, `Starting`, `Healthy`, `Degraded`, `Unhealthy`, `Recovering`); mặc định mọi prober `Disabled` (`HealthProber/HealthProberManager.cs:53,85,158`).
  - [x] **Cấu hình chuẩn hóa**: `HealthProberOptions` với Interval 60s, Timeout 30s, FailureThreshold 5, StartPeriod 60s, LogDirectory "logs/health", MaxLinesPerLogFile 1000 (đúng trong `appsettings.json`).
  - [x] **Logging phân tầng**:
    - Healthy: Không ghi file log, lưu mẫu trong Ring Buffer RAM & OpenTelemetry metrics.
    - Sự cố: `FileRollingHealthIncidentLogger` xoay file khi đạt ngưỡng 1000 dòng.
    - Định hướng tương lai: gom batch $\ge 1000$ dòng trước khi gửi LogService tập trung. ⚠️ `Logging/GatewayBatchLogSink.cs` đã POST tới `{RemoteEndpoint}/api/v1/log/batch` nhưng **endpoint này không tồn tại** (`Log.Api/Program.cs:66-67` chỉ có `/health` và `/`), response bị bỏ qua và `catch { }` rỗng ⇒ log batch mất im lặng.
  - [x] **Circuit Breaker Fast-Fail**: `GatewayDispatcher.cs:103-113` / `:164-176` trả HTTP 503 `DOWNSTREAM_UNHEALTHY` khi `IsServiceAvailable(service) == false`.
  - [ ] **Docker Container Recovery & Saga Rollback**: ❌ **Saga chưa có** — `HealthProber/SagaRollbackPublisher.cs` chỉ ghi warning log rồi `Task.CompletedTask`, không có MassTransit/RabbitMQ trong gateway (0 package). ⚠️ **Docker recovery chạy bằng CLI**: `DockerContainerRecoveryService.cs` gọi `docker restart <container>` qua `ProcessStartInfo`; compose có mount `/var/run/docker.sock` nhưng image `mcr.microsoft.com/dotnet/aspnet:8.0` **không có docker CLI** ⇒ không hoạt động trong container.
  - [x] **Controller Quản Trị (Controller thứ 15)**: `HealthProberController` với 5 action — `GET /api/v1/gateway/prober/status`, `GET .../status/{service}`, `PUT .../{service}`, `PUT .../bulk`, `POST .../{service}/check` (`HealthProberController.cs:33,51,79,110,124`). *(Tài liệu cũ gọi là "11th Controller".)*
  - [ ] **Kiểm thử tự động**: `HealthProberUnitTests.cs` (2 `[Fact]`) và `HealthProberControllerTests.cs` (4 `[Fact]`) **có thật**, nhưng mốc "**111/111 tests PASS**" là số bịa/không tái lập được — đã xoá.
- **Plan 9: Xây Dựng Bộ Chuyển Đổi Enum Hai Chiều & Danh Mục Thực Thể Chuẩn Form**
  - [x] **PascalCaseJsonStringEnumConverter**: `Common/Converters/PascalCaseJsonStringEnumConverter.cs:13`, đăng ký tại `Extensions/CoreServicesExtension.cs:34,40` với `allowIntegerValues: false`.
  - [x] **Validate Enum Input**: HTTP 400 ProblemDetails kèm danh sách giá trị hợp lệ (nhánh enum trong converter).
  - [ ] **Đặc tả danh mục cho Frontend**: `docs/FRONTEND_ENTITY_AND_ENUM_CATALOG.md` **có tồn tại nhưng nằm ngoài repo này** (thư mục `docs/` của monorepo root). Repo gateway là self-contained nên **không** link ra ngoài; tài liệu này không thuộc phạm vi gateway.
  - [ ] **Kiểm thử tự động**: `EnumConversionTests.cs` **có thật** (4 `[Fact]` + 1 `[Theory]`); mốc "**120/120 tests PASS**" **không tái lập được** — đã xoá. Quy mô thật của toàn bộ test project: 62 attribute / 19 file.
---
## 0.5. Đối chiếu với code hiện tại (2026-10-24)
| Hạng mục | Tài liệu cũ ghi | Code thật | Nhãn |
|:---|:---|:---|:---|
| Số controller | 10 ("11th Controller") | 15 file, **4 có action**, 11 stub 10 dòng | ⚠️ MỘT PHẦN |
| Số action | (không nêu) | 16 (`GET` 5, `POST` 6, `PUT` 5) | ✅ CÓ |
| gRPC client | (không nêu) | 1/8 — chỉ `users` (`Extensions/GrpcClientsExtension.cs`) | ⚠️ MỘT PHẦN |
| DTOs | "toàn bộ 10 domains" | 4/8: Auth, GatewayControl, HealthProber, Users | ⚠️ MỘT PHẦN |
| Cổng live run | 5200 | 8000 (port bất biến) | ✅ CÓ |
| Tên mode | `GatewayMode` | `AuthMode` (`Enforce`/`Standard`/`Bypass`), `GatewayMode` = alias legacy | ⚠️ MỘT PHẦN |
| Runtime manager API | `GetGatewayMode`/`SetGatewayModeAsync`/`IsAuthBypassed` | `GetAuthMode`/`SetAuthModeAsync`/`GetServiceModes`/`SetServiceModeAsync`/`SetBulkServiceModesAsync`/`SetBulkModesAsync` | ✅ CÓ |
| Số test | 46/46, 40, 111/111, 120/120 | 62 attribute / 19 file (49 `[Fact]` + 13 `[Theory]`) | ❌ Số cũ sai |
| Login mock sinh token | ✅ | Action có, `rpc Login` backend chưa implement | ⚠️ MỘT PHẦN |
| Saga Rollback | Publish RabbitMQ | Chỉ log + `Task.CompletedTask` | ❌ CHƯA CÓ |
| Docker recovery | Restart/recreate qua socket | `docker restart` CLI, image không có CLI | ⚠️ MỘT PHẦN |
| Batching log | Gửi LogService | POST tới endpoint 404, `catch { }` rỗng | ⚠️ MỘT PHẦN |
| Endpoint E2E `/users/me`, `/seller/inventory`, `/admin/roles` | ✅ 200 | `/api/v1/users/me` ✅; hai route kia **không tồn tại** (stub route là `api/v1/catalog/inventory`, `api/v1/admin/users`) | ❌ SAI |