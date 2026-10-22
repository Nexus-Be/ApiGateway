# PLAN — Kế hoạch & Tiến trình Mở rộng Nexus API Gateway (Gateway Mode State)

Tài liệu này ghi nhận kế hoạch tổng thể, các kế hoạch con (sub-plans), tiêu chí nghiệm thu (checkpoints), bằng chứng kiểm thử từng phần và kết quả chạy thử nghiệm thực tế (live run E2E) bổ sung trạng thái Gateway Mode linh hoạt giữa kiểm tra đăng nhập bình thường và chế độ Mock độc lập.

---

## 0. Đối chiếu Yêu cầu Nghiệp vụ

1. **Trạng thái Mặc định (`GatewayMode: "Standard"`)**:
   - Vẫn cần đăng nhập và kiểm tra quyền mọi thứ như thường.
   - Chưa đăng nhập (không gửi token) ➔ Bị chặn với mã `401 Unauthorized`.
   - Đăng nhập (`POST /gateway/api/v1/auth/login`) lấy token ➔ Gửi token vào request để truy cập `200 OK`.
   - Sai vai trò/quyền (ví dụ buyer gọi endpoint admin) ➔ Bị chặn với mã `403 Forbidden`.
2. **Trạng thái Mock (`GatewayMode: "Bypass"`)**:
   - Bật khi team muốn phát triển giao diện nhanh hoặc test độc lập.
   - **KHÔNG CHECK AUTH**: Không bắt buộc đăng nhập, tự động cấp quyền tương ứng cho các API.
   - **CÁC API ĐỘC LẬP**: Các API mock chạy hoàn toàn độc lập mà không bị ràng buộc vào login hay service user.
   - Dễ dàng bật/tắt qua file cấu hình `gateway.runtime.json` hoặc gọi API `PUT /gateway/api/v1/gateway/mode`.

---

## 1. Kế hoạch Tổng thể & Các Kế hoạch Con (Phases)

- **Plan 1: Bổ sung trạng thái GatewayMode trong GatewayOptions, config và IGatewayRuntimeManager**
  - [x] Thêm thuộc tính `GatewayMode` (`"Standard"` | `"Bypass"`) vào `GatewayOptions` và `gateway.runtime.json`.
  - [x] Cập nhật `IGatewayRuntimeManager` và `GatewayRuntimeManager` quản lý `GetGatewayMode`, `SetGatewayModeAsync`, `IsAuthBypassed`.
  - *Checkpoint*: Build thành công.

- **Plan 2: Cập nhật Security theo 2 trạng thái**
  - [x] Khi `Standard`: Giữ nguyên kiểm tra đăng nhập và phân quyền JWT/RBAC bình thường.
  - [x] Khi `Bypass`: Tự động nới lỏng auth và cấp quyền thích ứng để các API mock chạy độc lập.
  - *Checkpoint*: Build thành công.

- **Plan 3: Nâng cấp API Gateway Modes**
  - [x] `GET /gateway/api/v1/gateway/modes`: Trả về cả `gatewayMode` và danh sách `services`.
  - [x] `PUT /gateway/api/v1/gateway/mode`: Cho phép bật/tắt runtime giữa `Standard` và `Bypass` (`{"mode": "Bypass"}`).
  - [x] `PUT /gateway/api/v1/gateway/modes`: Hỗ trợ cập nhật đồng thời nhiều service và gateway mode.
  - *Checkpoint*: Build thành công.

- **Plan 4: Nâng cấp Mock Login sinh token theo tài khoản**
  - [x] `POST /gateway/api/v1/auth/login` trả về token hợp lệ cho mock flow.
  - *Checkpoint*: Build thành công.

- **Plan 5: Viết Test Suite tự động kiểm chứng cả 2 trạng thái**
  - [x] Bổ sung `Gateway_Standard_mode_strictly_requires_login_as_normal` và `Gateway_Bypass_mode_skips_auth_check_and_runs_independently`.
  - [x] Chạy `dotnet test ApiGateway.sln -c Release`: 46/46 tests pass 100%.
  - *Checkpoint*: Toàn bộ 40 tests vượt qua.

- **Plan 6: Chạy live server Gateway, test trực tiếp cả 2 luồng và nghiệm thu**
  - [x] Chạy live server Gateway thực tế cổng 5200:
    - Luồng Standard: Gọi không token ➔ 401; Đăng nhập lấy token ➔ 200 OK.
    - Luồng Bypass: Đổi mode sang `Bypass` ➔ Gọi `/users/me`, `/seller/inventory`, `/admin/roles` không cần token đều đạt 200 OK độc lập.
    - Reset về Standard: Gọi lại không token ➔ Lập tức bị chặn 401 như thường.

- **Plan 7: Tái cấu trúc 3 Khối Độc Lập (3-Tier Decoupling: Input → Middle → Output)**
  - [x] **Tầng Common & Responses**: Chuẩn hóa `ApiResponseFactory`, `ValidationHelpers`, `HttpContextUserExtensions`, attributes.
  - [x] **Khối Phần Giữa (Dispatching Core)**: Xây dựng `ModeEvaluator`, `MockDataMapper`, `GrpcServiceCaller`, `GatewayDispatcher`, `BackendDispatcher`.
  - [x] **Dữ Liệu Seed (`Seeds/`)**: Tổ chức `Seeds/{service}/{entity}.json`, bảo đảm Gateway không giữ stateful DB in-memory, thẩm định contract qua Protobuf MessageParser.
  - [x] **Tách biệt DTOs (`Dtos/`)**: Chuyển từ Models sang DTOs, tách thành `*Requests.cs` và `*Responses.cs` cho toàn bộ 10 domains.
  - [x] **Khối Đầu Vào (Controllers)**: Xây dựng 10 Controllers tiêu chuẩn `[ApiController] ControllerBase` trong `Controllers/`, loại bỏ hoàn toàn thư mục `Endpoints/` cũ.
  - [x] **Tích hợp DI & Route**: Cấu hình `services.AddControllers()` và `app.MapControllers()`, Program.cs siêu tinh gọn (< 35 dòng).
  - [x] **Kiểm thử tự động**: Chạy toàn bộ test suite `dotnet test ApiGateway.sln` đạt 46/46 tests PASS 100%.
  - [x] **Đồng bộ tài liệu**: Cập nhật `dac_ta.md`, `DEV_GUIDE.md`, `README.md`, `PLAN.md`.

- **Plan 8: Xây dựng Hệ Thống Health Prober Daemon, Giám Sát Sức Khỏe & Tự Hồi Phục (Self-Healing)**
  - [x] **State Machine**: Thiết kế State Machine quản lý 6 trạng thái (`Disabled`, `Starting`, `Healthy`, `Degraded`, `Unhealthy`, `Recovering`).
  - [x] **Cấu hình chuẩn hóa**: Bổ sung `HealthProberOptions` với Interval 60s, Timeout 30s, Retries 5, StartPeriod 60s, LogDirectory "logs/health", MaxLinesPerLogFile 1000.
  - [x] **Logging phân tầng**:
    - Healthy: Không ghi file log, lưu 30 mẫu trong Ring Buffer RAM & OpenTelemetry metrics.
    - Sự cố: Xây dựng `FileRollingHealthIncidentLogger` tự động xoay file khi đạt ngưỡng 1000 dòng.
    - Định hướng tương lai: Quy chuẩn gom batch $\ge 1000$ dòng trước khi gửi LogService tập trung.
  - [x] **Circuit Breaker Fast-Fail**: Can thiệp `GatewayDispatcher`, ngắt mạch 0ms trả về HTTP 503 `DOWNSTREAM_UNHEALTHY` khi downstream Unhealthy/Recovering.
  - [x] **Docker Container Recovery & Saga Rollback**: Tự động restart/recreate container qua Docker socket và bắn sự kiện `ServiceUnhealthyIntegrationEvent` lên RabbitMQ.
  - [x] **Controller Quản Trị Mới (11th Controller)**: Xây dựng `HealthProberController` với các API tra cứu status, bật/tắt daemon từng service & bulk, và endpoint kiểm tra sức khỏe thủ công tức thì (`POST /api/v1/gateway/prober/{service}/check`).
  - [x] **Kiểm thử tự động toàn diện**: Bổ sung `HealthProberUnitTests.cs` và `HealthProberControllerTests.cs`, đạt **111/111 tests PASS 100%**.

- **Plan 9: Xây dựng Bộ Chuyển Đổi Enum Hai Chiều & Danh Mục Thực Thể Chuẩn Form**
  - [x] **PascalCaseJsonStringEnumConverter**: Tự động chuyển đổi enum số trong DB sang chuỗi PascalCase cho Frontend, và parse ngược lại từ chuỗi FE sang enum số Backend.
  - [x] **Validate Enum Input**: Trả về lỗi HTTP 400 ProblemDetails khi FE gửi sai enum kèm danh sách gợi ý giá trị hợp lệ.
  - [x] **Đặc tả danh mục cho Frontend**: Xuất bản `docs/FRONTEND_ENTITY_AND_ENUM_CATALOG.md` với đầy đủ 80 Enums thuộc 8 Services.
  - [x] **Kiểm thử tự động**: Bổ sung `EnumConversionTests.cs`, nâng tổng số bài test lên **120/120 tests PASS 100%**.

