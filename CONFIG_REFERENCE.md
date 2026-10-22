# CẨM NANG CẤU HÌNH & BIẾN MÔI TRƯỜNG NEXUS API GATEWAY (CONFIG_REFERENCE.md)

Tài liệu này giải thích chi tiết ý nghĩa của **từng file cấu hình**, **từng biến môi trường**, **nhiệm vụ của chúng** và **thứ tự ưu tiên khi nạp cấu hình (Configuration Precedence)** trong Nexus API Gateway (.NET 8).

---

## 1. BẢN ĐỒ CÁC FILE CẤU HÌNH TRONG DỰ ÁN

| File | Vị trí | Mục đích sử dụng | Được commit lên Git? |
|:---|:---|:---|:---:|
| `appsettings.json` | `Nexus.Gateway/` | Cấu hình cơ sở (Base/Production). Chứa các giá trị mặc định cho toàn hệ thống với toàn bộ 8 services ở mode `Real`. | ✅ Có |
| `appsettings.Development.json` | `Nexus.Gateway/` | Cấu hình cho môi trường phát triển cục bộ (Local Development). Ghi đè các service sang chế độ `Mock`. | ✅ Có |
| `appsettings.example.json` | `Nexus.Gateway/` | File mẫu tham khảo cấu hình cơ sở cho team. | ✅ Có |
| `gateway.runtime.json` | `Nexus.Gateway/` | File runtime cấu hình động (hỗ trợ **Hot-Reload tức thì** khi bật/tắt Mock hoặc đổi mode qua API/file). | ✅ Có |
| `gateway.runtime.example.json`| `Nexus.Gateway/` | File mẫu runtime cấu hình động. | ✅ Có |
| `launchSettings.json` | `Nexus.Gateway/Properties/` | Cấu hình cổng chạy khi khởi động local qua Visual Studio, Rider hoặc `dotnet run`. | ✅ Có |
| `.env.example` | `ApiGateway/` | File mẫu danh sách các biến môi trường (Environment Variables) cho server/Docker. | ✅ Có |
| `.env` | `ApiGateway/` | File chứa biến môi trường thực tế và bí mật (Secret Key). | ❌ Không (chặn trong `.gitignore`) |

---

## 2. THỨ TỰ ƯU TIÊN NẠP CẤU HÌNH (CONFIGURATION PRECEDENCE)

Cấu hình .NET được nạp từ `appsettings.json` → `appsettings.{Environment}.json` → `gateway.runtime.json` → biến môi trường (nguồn sau ghi đè nguồn trước). **Request headers không phải configuration provider**: chỉ ở Development và khi bật `AllowClientModeOverrideInDevelopment`, header có thể ghi đè *mode hiệu dụng của request hiện tại*. Các lệnh chuyển mode qua admin API còn tạo memory override, có ưu tiên hơn mode cấu hình khi không có header hợp lệ.

Hai loại mode độc lập:
- **Service mode `Mock` / `Real`**: header `X-Service-Mode-{service}` → `X-Service-Modes` → `X-Mock-Mode` → `X-Gateway-Mode-Override` → memory override → `Gateway:Services:<service>:Mode`.
- **Auth mode `Standard` / `Bypass`**: header `X-Gateway-Auth` → `X-Gateway-Mode` → memory override → `Gateway:AuthMode`/`Gateway:GatewayMode`. `Bypass` chỉ có hiệu lực trong Development đối với endpoint được phép và service ở Mock; control-plane không được bypass.

Dùng admin API để xem/đổi mode chung, dùng header cho một request test trong Development. Không lấy service mode để suy ra auth mode hoặc ngược lại.

---

## 3. CHI TIẾT TỪNG PHẦN CẤU HÌNH & BIẾN MÔI TRƯỜNG

### A. Cấu Hình Xác Thực JWT (`Jwt`)
Đặt tại: `appsettings.json`, `appsettings.Development.json`, hoặc qua biến môi trường `Jwt__*`.

| Khóa cấu hình (JSON) | Biến môi trường tương ứng | Kiểu | Mặc định | Ý nghĩa & Tác dụng |
|:---|:---|:---:|:---:|:---|
| `Jwt:Issuer` | `Jwt__Issuer` | `string` | `http://localhost:8001` | **Nguồn phát hành token**. URL của Identity/UserService cấp phát token. Gateway kiểm tra claim `iss`. |
| `Jwt:Authority` | `Jwt__Authority` | `string` | `http://localhost:8001` | **Địa chỉ máy chủ OIDC**. Tương đương với Issuer khi chạy cục bộ. |
| `Jwt:Audience` | `Jwt__Audience` | `string` | `shoppingcart-api` | **Đối tượng nhận token**. Mã định danh API mà token này hướng tới. Gateway kiểm tra claim `aud`. |
| `Jwt:Signature` | `Jwt__Signature` | `string` | *(Bắt buộc)* | **Khóa bí mật ký đối xứng (Signing Key)**. Chuỗi tối thiểu 32 ký tự (256 bits) cho thuật toán HMAC-SHA256 để xác thực chữ ký token. Hỗ trợ alias: `Jwt:SigningKey`, `Jwt:Key`. |
| `Jwt:ExpirationInMinutes` | `Jwt__ExpirationInMinutes` | `int` | `60` (Prod) / `120` (Dev) | **Thời hạn sống của token** tính bằng phút. Hỗ trợ alias: `Jwt:Expiration`. |
| `Jwt:RequireHttpsMetadata` | `Jwt__RequireHttpsMetadata` | `bool` | `false` (Dev) / `true` (Prod) | Bắt buộc HTTPS khi kiểm tra metadata của nhà phát hành. |

---

### B. Cấu Hình Hoạt Động Của Gateway (`Gateway`)
Đặt tại: `appsettings.json`, `appsettings.Development.json`, `gateway.runtime.json`, hoặc biến môi trường `Gateway__*`.

| Khóa cấu hình (JSON) | Biến môi trường tương ứng | Giá trị mẫu | Ý nghĩa & Tác dụng |
|:---|:---|:---:|:---|
| `Gateway:AuthMode` | `Gateway__AuthMode` | `"Standard"` / `"Enforce"` hoặc `"Bypass"` | **Chế độ kiểm tra xác thực của Gateway**:<br>- `"Standard"` hoặc `"Enforce"`: Yêu cầu JWT và kiểm tra quyền RBAC nghiêm ngặt (chặn 401/403).<br>- `"Bypass"`: (Development-only) Tự động nới lỏng auth để Frontend test độc lập với Mock API. |
| `Gateway:PermitLimit` | `Gateway__PermitLimit` | `100` | Số lượng request tối đa được phép trong một cửa sổ thời gian cố định (Redis fixed-window rate limit). |
| `Gateway:WindowSeconds` | `Gateway__WindowSeconds` | `60` | Thời gian của cửa sổ rate limit (tính bằng giây). Mặc định `100` requests / `60` giây. |
| `Gateway:ConcurrencyLimit` | `Gateway__ConcurrencyLimit` | `100` | Số lượng request đồng thời tối đa Gateway tiếp nhận cùng một thời điểm. |
| `Gateway:RedisConnectionString` | `Gateway__RedisConnectionString` | `"localhost:6379"` | Địa chỉ kết nối Redis phục vụ tính năng Distributed Rate Limiting. |
| `Gateway:RedisKeyPrefix` | `Gateway__RedisKeyPrefix` | `"nexus:gateway"` | Tiền tố cho các khóa (keys) lưu trong Redis để tránh xung đột với ứng dụng khác. |
| `Gateway:RedisFailOpenInDevelopment` | `Gateway__RedisFailOpenInDevelopment` | `true` | Nếu bật `true` ở Dev: Nếu Redis bị sập hoặc chưa bật, Gateway tự động cho request đi qua và log cảnh báo, không làm gián đoạn việc code. |
| `Gateway:CorsOrigins` | `Gateway__CorsOrigins__0`, `__1` | `["http://localhost:3000", ...]` | Danh sách URL Frontend được phép gọi API (tránh lỗi CORS Policy của trình duyệt). |
| `Gateway:CorsMethods` | `Gateway__CorsMethods` | `["GET", "POST", ...]` | Các phương thức HTTP được phép qua CORS. |
| `Gateway:CorsHeaders` | `Gateway__CorsHeaders` | `["*"]` | Các HTTP Header được phép gửi lên Gateway. |
| `Gateway:CorsAllowCredentials` | `Gateway__CorsAllowCredentials` | `true` | Cho phép gửi Cookie/Credentials qua CORS. |
| `Gateway:AllowClientModeOverrideInDevelopment` | `Gateway__AllowClientModeOverrideInDevelopment` | `true` | Cho phép Client tự đổi mode Mock/Real qua Header (ví dụ: `X-Service-Mode-catalog: Real`). |
| `Gateway:AllowAnonymousMockAuthInDevelopment` | `Gateway__AllowAnonymousMockAuthInDevelopment` | `true` | Cho phép gọi API không cần token khi Gateway ở chế độ Bypass. |
| `Gateway:AllowMockTokenAuth` | `Gateway__AllowMockTokenAuth` | `true` | Cho phép nhập các từ khóa `admin`, `seller`, `staff`, `buyer` trên Swagger thay cho token JWT thật khi test Mock. |
| `Gateway:DefaultMockUserId` | `Gateway__DefaultMockUserId` | `"11111111-1111-1111-1111-000000000004"` | Mã UUID user mặc định gán cho các request mock khi không có token trong mode Bypass. |
| `Gateway:DefaultMockRole` | `Gateway__DefaultMockRole` | `"buyer"` | Quyền mặc định gán cho request mock trong mode Bypass. |

---

### C. Cấu Hình 8 Microservices Upstream (`Gateway:Services:<name>`)
Mỗi service được cấu hình với 5 thuộc tính:

| Thuộc tính | Kiểu | Ý nghĩa | Mặc định | Ví dụ |
|:---|:---:|:---|:---:|:---|
| `Mode` | `string` | **`"Mock"`** (đọc file seed JSON) hoặc **`"Real"`** (gọi gRPC sang backend thật). | `"Real"` (Prod) / `"Mock"` (Dev) | `"Mock"` |
| `Address` | `string` | Địa chỉ gRPC của service backend thật. | *(Bắt buộc)* | `"http://localhost:8001"` |
| `TimeoutSeconds` | `int` | Thời gian tối đa chờ backend phản hồi trước khi trả về lỗi 504 Gateway Timeout. | `5` | `5` (giây) |
| `MockDelayMs` | `int` | Giả lập độ trễ mạng khi ở chế độ Mock (tính bằng mili-giây) để test loading UI. | `0` | `500` (ms) |
| `MockStatus` | `int` | Giả lập HTTP status code trả về khi ở Mock để test lỗi mạng. | `200` | `200` (hoặc `503`) |

#### Danh Sách 8 Microservices Đã Quy Hoạch Chuẩn:
- **`users`**: `http://localhost:8001` (User, Auth, Profile, RBAC)
- **`catalog`**: `http://localhost:8002` (Danh mục, Sản phẩm, Tồn kho ảo)
- **`commerce`**: `http://localhost:8003` (Giỏ hàng, Đơn hàng, Checkout)
- **`log`**: `http://localhost:8004` (Nhật ký hệ thống tập trung, Event Types)
- **`auction`**: `http://localhost:8005` (Phòng đấu giá, Đặt giá bid)
- **`fulfillment`**: `http://localhost:8006` (Kho bãi vật lý, Tính phí ship, Vận đơn)
- **`notification`**: `http://localhost:8007` (Thông báo, Cài đặt thông báo)
- **`wallet`**: `http://localhost:8008` (Ví điện tử, Số dư, Nạp/rút/chuyển tiền)

---

### D. Cấu Hình Kestrel Server & Observability

| Khóa / Biến môi trường | Vị trí cấu hình | Giá trị | Ý nghĩa |
|:---|:---|:---:|:---|
| `MaxRequestBodySize` | `Program.cs` | `65536` (64KB) | Giới hạn kích thước gói tin gửi lên Gateway, chống làm tràn RAM server. |
| `RequestTimeout` | `CoreServicesExtension.cs` | `65` (giây) | Tự động ngắt kết nối và trả về 504 nếu request bị treo quá 65 giây. |
| `ASPNETCORE_ENVIRONMENT` | `.env` / `launchSettings.json` | `Development` / `Production` | Xác định môi trường chạy của Gateway. |
| `ASPNETCORE_URLS` | `.env` / `Dockerfile` | `http://+:8000` | Cổng HTTP mà Gateway lắng nghe (Port `8000`). |
| `OTEL_EXPORTER_OTLP_ENDPOINT`| `.env` / Environment | `http://localhost:4317` | Địa chỉ nhận dữ liệu OpenTelemetry Traces & Metrics (Grafana Tempo / OTLP Collector). Hỗ trợ alias `Otel:Endpoint` trong appsettings. |

---

### E. Cấu Hình Giám Sát Sức Khỏe & Tự Hồi Phục (`Gateway:HealthProber`)
Đặt tại: `appsettings.json`, `appsettings.Development.json` hoặc qua biến môi trường `Gateway__HealthProber__*`.

| Khóa cấu hình (JSON) | Biến môi trường tương ứng | Kiểu | Mặc định | Ý nghĩa & Tác dụng |
|:---|:---|:---:|:---:|:---|
| `Gateway:HealthProber:IntervalSeconds` | `Gateway__HealthProber__IntervalSeconds` | `int` | `60` | Chu kỳ định kỳ mỗi lần Prober Daemon gửi request probe (tính bằng giây). Mặc định 1 phút. |
| `Gateway:HealthProber:TimeoutSeconds` | `Gateway__HealthProber__TimeoutSeconds` | `int` | `30` | Thời gian tối đa chờ phản hồi HTTP /health từ backend service trước khi tính là probe timeout. |
| `Gateway:HealthProber:FailureThreshold` | `Gateway__HealthProber__FailureThreshold` | `int` | `5` | Số lần thất bại liên tiếp trước khi chuyển trạng thái sang `Unhealthy` và kích hoạt Circuit Breaker. |
| `Gateway:HealthProber:StartPeriodSeconds` | `Gateway__HealthProber__StartPeriodSeconds` | `int` | `60` | Thời gian ân hạn khi container mới khởi động lại (không tính lỗi trong khoảng này để container kịp nạp runtime/DB). |
| `Gateway:HealthProber:AutoRecoverContainer` | `Gateway__HealthProber__AutoRecoverContainer` | `bool` | `true` | Tự động kích hoạt chuỗi Stop $\rightarrow$ Remove $\rightarrow$ Recreate container qua Docker socket khi đạt ngưỡng Unhealthy. |
| `Gateway:HealthProber:MaxAutoRecoverAttempts` | `Gateway__HealthProber__MaxAutoRecoverAttempts` | `int` | `3` | Giới hạn số lần tự động hồi phục liên tiếp để chống CrashLoopBackOff làm treo tài nguyên máy chủ. |
| `Gateway:HealthProber:LogDirectory` | `Gateway__HealthProber__LogDirectory` | `string` | `"logs/health"` | Thư mục lưu trữ tệp log sự cố sức khỏe (Incident log) tại Gateway. |
| `Gateway:HealthProber:MaxLinesPerLogFile` | `Gateway__HealthProber__MaxLinesPerLogFile` | `int` | `1000` | Số dòng tối đa trên một tệp log sự cố trước khi tự động xoay sang tệp mới (`incident-YYYYMMDD-XXXX.log`). |

---

### F. Danh Sách Request Headers Ghi Đè (Header Overrides)

Trong môi trường Development, Gateway hỗ trợ ghi đè cấu hình cho từng request thông qua các HTTP Headers:

| Header Name | Giá Trị Hợp Lệ | Mục Đích Sử Dụng |
|:---|:---:|:---|
| `X-Service-Mode-{service}` | `Real` hoặc `Mock` | Ép buộc một service cụ thể chạy `Real` hoặc `Mock` cho riêng request đó (ví dụ: `X-Service-Mode-catalog: Real`). |
| `X-Gateway-Auth` | `Bypass` hoặc `Standard` | Tạm thời bật/tắt chế độ bypass auth cho riêng request đó. |
| `X-Mock-Enforce-Auth` | `true` | Ép buộc kiểm tra auth nghiêm ngặt ngay cả khi service đang ở chế độ Mock. |
| `X-Require-Real-Auth` | `true` | Bắt buộc phải có JWT Token thật từ backend `UserService:8001`, chặn hoàn toàn mock token. |
| `X-Mock-Role` | `admin`, `seller`, `staff`, `buyer` | Chỉ định vai trò giả lập mong muốn khi gửi mock token. |
| `X-Mock-User-Id` | UUID string | Chỉ định UserId giả lập mong muốn khi gửi mock token. |

---

## 4. HƯỚNG DẪN THỰC HÀNH CẤU HÌNH DÀNH CHO DEVELOPER

1. **Khi chạy local để code giao diện hoặc test độc lập với Mock**:
   - Sử dụng file `appsettings.Development.json`.
   - Toàn bộ 8 services đã được bật sẵn `"Mode": "Mock"`. Bạn chỉ việc chạy `./run-dev.sh` (hoặc `dotnet run --project Nexus.Gateway`) là toàn bộ API đều hoạt động mượt mà.
   - Nếu muốn test không cần đăng nhập: Mở `gateway.runtime.json`, đổi `"AuthMode": "Bypass"`.

2. **Khi code xong 1 microservice (ví dụ Catalog Service)**:
   - Khởi động `Catalog Service` thật ở cổng `http://localhost:8002`.
   - Mở file `gateway.runtime.json`, sửa:
     ```json
     "catalog": {
       "Mode": "Real",
       "Address": "http://localhost:8002"
     }
     ```
   - **Lưu file là có hiệu lực ngay (Hot-Reload), KHÔNG cần khởi động lại Gateway!**
   - Gateway sẽ tự động chuyển tiếp request tới `http://localhost:8002` qua gRPC, trong khi 7 service còn lại vẫn chạy Mock bình thường.

3. **Khi triển khai Docker / Production**:
   - Tạo file `.env` từ `.env.example`.
   - Điền khóa bí mật `Jwt__Signature` thật dài tối thiểu 32 ký tự.
   - Đặt `ASPNETCORE_ENVIRONMENT=Production`.
   - Toàn bộ 8 services trong `appsettings.json` đều mặc định ở mode `Real` và chế độ `AuthMode` là `Enforce` nghiêm ngặt.
