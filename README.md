# Nexus API Gateway (.NET 8)

HTTP gateway gồm **15 controller** và cấu hình **8 backend service**: `users`, `catalog`, `commerce`, `log`, `auction`, `fulfillment`, `notification`, `wallet`. Controller dispatch qua gRPC khi service ở `Real`, hoặc đọc fixture protobuf JSON trong `Nexus.Gateway/Seeds/` khi ở `Mock`. `Nexus.Gateway/Mocks/*.mock.json` là **declarative mock routes** riêng: phục vụ Mock, không tự tạo RPC hay route Real. Xem [DEV_GUIDE.md](DEV_GUIDE.md) để thêm API và [MOCK_GUIDE.md](MOCK_GUIDE.md) để biết cách tạo dữ liệu mock trong 30 giây.

## Chạy và kiểm thử

Từ thư mục `src/Services/ApiGateway`:

```bash
dotnet test ApiGateway.sln -c Release
./run-dev.sh # Unix; PowerShell dùng ./run-dev.ps1
```

Hoặc chạy `docker compose up -d` theo cấu hình Compose để khởi chạy trọn bộ hạ tầng: Gateway (port 8000), Redis (port 6379), Grafana Tempo (port 4317/3200) và Grafana Dashboard (port 3000 tại `http://localhost:3000/explore`). Kiểm tra địa chỉ Gateway trong script/launch profile hoặc cấu hình đang dùng; Swagger (`/swagger`) chỉ bật ở Development. Không giả định backend Real sẵn sàng: phải triển khai RPC tương ứng và cấu hình địa chỉ trước khi gọi.

## Giám sát sức khỏe & Tự phục hồi (Health Prober Daemon)

Gateway tích hợp sẵn **Health Prober Daemon Engine** giám sát định kỳ 8 backend services:
- **Chu kỳ & Ngưỡng**: `Interval: 60s` (1 phút), `Timeout: 30s`, `Retries: 5 lần`, `StartPeriod: 60s` ân hạn khởi động.
- **State Machine**: `Disabled` (mặc định) $\rightarrow$ `Starting` $\rightarrow$ `Healthy` $\leftrightarrow$ `Degraded` $\rightarrow$ `Unhealthy` $\rightarrow$ `Recovering`.
- **Cơ chế phản ứng khi Unhealthy**:
  - **Circuit Breaker Fast-Fail**: Ngắt mạch tức thì trong 0ms tại `GatewayDispatcher`, trả về HTTP 503 `DOWNSTREAM_UNHEALTHY`.
  - **Saga Rollback**: Bắn sự kiện `ServiceUnhealthyIntegrationEvent` lên RabbitMQ để hủy các giao dịch đang treo.
  - **Docker Auto-Recovery**: Tự động restart/recreate container `nexus-{service}-service` qua Docker socket (tối đa 3 lần chống lặp vô hạn).
- **Log sự cố**: Lưu tại `logs/health/incident-YYYYMMDD-XXXX.log` với cơ chế xoay file tối đa `1000` dòng/file. Healthy không ghi file log.
- **API Quản trị (Admin)**: `GET /api/v1/gateway/prober/status`, `PUT /api/v1/gateway/prober/{service}`, `PUT /api/v1/gateway/prober/bulk`, và `POST /api/v1/gateway/prober/{service}/check` (kiểm tra thủ công tức thì). Chi tiết xem [docs/HEALTH_AND_LOGGING_ARCHITECTURE.md](../../../docs/HEALTH_AND_LOGGING_ARCHITECTURE.md).

## Service mode và auth mode là hai trục khác nhau

Trong `Nexus.Gateway/gateway.runtime.json`, đặt `Gateway:Services:<service>:Mode` thành `Mock` hoặc `Real`, và `Gateway:AuthMode` thành `Enforce` (mặc định) hoặc `Bypass`. Có thể cập nhật qua control API `/api/v1/gateway/modes/{service}` và `/api/v1/gateway/mode` với **quyền Gateway admin**; endpoint control có `[DisableMockAuthorizationBypass]`. Override theo request header chỉ hoạt động ở Development khi cấu hình cho phép. File cấu hình đăng ký reload khi thay đổi, nhưng nên xác minh mode có hiệu lực bằng request thay vì giả định hot reload tức thì.

`Enforce` giữ xác thực/phân quyền cho cả request Mock và Real. `Bypass` chỉ có hiệu lực trong **Development**, trên request Mock đủ điều kiện khi tùy chọn anonymous mock auth bật; không áp dụng vô điều kiện, không bỏ qua endpoint bị cấm bypass. Security và authorization được xử lý trước controller/mock response. Một declarative-only route khi service chuyển sang `Real` mà không có controller route tương ứng sẽ trả **404**, không tự forward sang backend.

Response controller thành công là envelope `success`, `statusCode`, `data`, `traceId`, `timestamp` (có thể có `message`), HTTP **200** theo mặc định; dispatcher hỗ trợ HTTP **201** khi endpoint khai báo rõ. Lỗi dùng shape `type`, `title`, `status`, `errorCode`, `detail`, `traceId`, `timestamp` (có thể có `errors`), HTTP status tương ứng; Exception Handler trung tâm tự động map lỗi và trả về đúng status code mà không cần controller try-catch hay gọi .Error(...) thủ công. Declarative `raw` cố ý trả body nguyên bản. Xem [DEV_GUIDE.md](DEV_GUIDE.md) để biết cách viết action siêu tinh gọn, phân trang, validation và phân biệt fixture/declarative mock.
