# CẨM NANG CẤU HÌNH & BIẾN MÔI TRƯỜNG NEXUS API GATEWAY (CONFIG_REFERENCE.md)
> **Trạng thái triển khai (2026-10-24):** ⚠️ Một phần — cấu hình, precedence, header override và rate limit đều có thật trong code; nhưng chỉ **1/8** service có gRPC client (`users`), nên chuyển service khác sang `Mode: "Real"` sẽ **không** gọi được backend. Các file `appsettings.json` / `appsettings.Development.json` **không được commit** (bị `.gitignore:81` chặn).
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-24.
Tài liệu này giải thích chi tiết ý nghĩa của **từng file cấu hình**, **từng biến môi trường**, **nhiệm vụ của chúng** và **thứ tự ưu tiên khi nạp cấu hình (Configuration Precedence)** trong Nexus API Gateway (.NET 8). Mọi khoá dưới đây được đối chiếu với `Nexus.Gateway/GatewayOptions.cs` và `Nexus.Gateway/Extensions/ConfigurationExtensions.cs`.
---
## 1. BẢN ĐỒ CÁC FILE CẤU HÌNH TRONG DỰ ÁN
| File | Vị trí | Mục đích sử dụng | Được commit lên Git? |
|:---|:---|:---|:---:|
| `appsettings.json` | `Nexus.Gateway/` | Cấu hình cơ sở (Base/Production). Toàn bộ 8 services ở `Mode: "Real"`, `AuthMode: "Enforce"`, các cờ bypass đều `false`. | ❌ **Không** — bị `.gitignore:81` (`appsettings*.json`) chặn |
| `appsettings.Development.json` | `Nexus.Gateway/` | Cấu hình cho môi trường phát triển cục bộ. Ghi đè 8 services sang `Mode: "Mock"`, bật các cờ bypass, JWT hạn 120 phút. | ❌ **Không** — cùng lý do trên |
| `appsettings.example.json` | `Nexus.Gateway/` | File mẫu cấu hình cơ sở cho team (JWT dùng placeholder, **không** chứa khoá thật). | ✅ Có |
| `appsettings.Development.example.json` | `Nexus.Gateway/` | File mẫu cấu hình Development. | ✅ Có |
| `gateway.runtime.json` | `Nexus.Gateway/` | File runtime cấu hình động, nạp **sau** appsettings và **có** `reloadOnChange: true` (sửa file là áp dụng ngay, không cần restart). | ✅ Có |
| `gateway.runtime.example.json`| `Nexus.Gateway/` | File mẫu runtime cấu hình động. | ✅ Có |
| `launchSettings.json` | `Nexus.Gateway/Properties/` | Cấu hình cổng chạy khi khởi động local qua Visual Studio, Rider hoặc `dotnet run` (`http://localhost:8080`, env `Development`). | ✅ Có |
| `.env.example` | `ApiGateway/` | File mẫu danh sách biến môi trường cho server/Docker (`GATEWAY_PORT=8080`, `REDIS_CONNECTION_STRING=redis:6379`, `OTEL_EXPORTER_OTLP_ENDPOINT=http://tempo:4317`, 8 biến `*_SERVICE_URL`). | ✅ Có |
| `.env` | `ApiGateway/` | File biến môi trường thực tế khi chạy Docker/CI. | ❌ Không (chặn trong `.gitignore`) |
> ⚠️ **Đính chính so với bản cũ**: bảng trước đây ghi `appsettings.json` và `appsettings.Development.json` là "✅ Có" (được commit) — **sai**. `.gitignore:81` khai báo `appsettings*.json` và chỉ whitelist `!appsettings*.example.json` / `!appsettings.example.json` / `!appsettings.*.example.json`, nên hai file thật **chưa bao giờ** vào git (`git ls-files` không liệt kê chúng). Hệ quả: **không có bí mật nào bị commit** qua đường này, nhưng cũng **không ai clone được cấu hình chạy được** nếu chỉ clone repo — phải copy từ `*.example.json`.
---
## 2. THỨ TỰ ƯU TIÊN NẠP CẤU HÌNH (CONFIGURATION PRECEDENCE)
Cấu hình .NET được nạp theo đúng thứ tự sau (`Extensions/ConfigurationExtensions.cs:12-16`), **nguồn sau ghi đè nguồn trước**:
```csharp
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddJsonFile("gateway.runtime.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();
```
1. `appsettings.json` (base)
2. `appsettings.{Environment}.json` (Development/Production)
3. **`gateway.runtime.json`** ← nạp **sau** appsettings ⇒ mọi giá trị ở file này **thắng** appsettings
4. Biến môi trường (`Gateway__Services__users__Mode=Mock`, `Jwt__Signature=...`, …) ← **thắng tất cả**
Sau khi nạp, options được validate fail-fast lúc khởi động (`:19-23`):
```csharp
.Validate(x => x.IsValid(isDevelopment), "Invalid gateway options; Mock and plaintext upstreams are Development-only.")
.ValidateOnStart();
```
**Request headers không phải configuration provider**: chỉ ở Development và khi bật `AllowClientModeOverrideInDevelopment`, header có thể ghi đè *mode hiệu dụng của request hiện tại*. Các lệnh chuyển mode qua admin API còn tạo memory override, có ưu tiên hơn mode cấu hình khi không có header hợp lệ.
Hai loại mode độc lập:
- **Service mode `Mock` / `Real`**: header `X-Service-Mode-{service}` → `X-Service-Modes` → `X-Mock-Mode` → `X-Gateway-Mode-Override` → memory override → `Gateway:Services:<service>:Mode` (`Services/GatewayRuntimeManager.cs:110,124`).
- **Auth mode `Enforce` / `Standard` / `Bypass`**: header `X-Gateway-Auth` → `X-Gateway-Mode` → memory override → `Gateway:AuthMode`/`Gateway:GatewayMode` (`Services/GatewayRuntimeManager.cs:38`). `Bypass` chỉ có hiệu lực trong Development đối với endpoint được phép và service ở Mock; control-plane không được bypass.
Dùng admin API để xem/đổi mode chung, dùng header cho một request test trong Development. Không lấy service mode để suy ra auth mode hoặc ngược lại.
---
## 3. CHI TIẾT TỪNG PHẦN CẤU HÌNH & BIẾN MÔI TRƯỜNG
### A. Cấu Hình Xác Thực JWT (`Jwt`)
Đặt tại: `appsettings.json`, `appsettings.Development.json`, hoặc qua biến môi trường `Jwt__*`.
| Khóa cấu hình (JSON) | Biến môi trường tương ứng | Kiểu | Mặc định | Ý nghĩa & Tác dụng | Trạng thái | Bằng chứng |
|:---|:---|:---:|:---:|:---|:---|:---|
| `Jwt:Issuer` | `Jwt__Issuer` | `string` | `http://localhost:8001` | **Nguồn phát hành token**. Gateway kiểm tra claim `iss`. | ✅ CÓ | `appsettings.json`, `Security/SecurityConfiguration.cs` |
| `Jwt:Authority` | `Jwt__Authority` | `string` | `http://localhost:8001` | **Địa chỉ máy chủ OIDC**. Tương đương Issuer khi chạy cục bộ. | ✅ CÓ | `Security/SecurityConfiguration.cs` |
| `Jwt:Audience` | `Jwt__Audience` | `string` | `shoppingcart-api` | **Đối tượng nhận token**. Gateway kiểm tra claim `aud`. | ✅ CÓ | `Security/SecurityConfiguration.cs` |
| `Jwt:Signature` | `Jwt__Signature` | `string` | *(Bắt buộc)* | **Khóa bí mật ký đối xứng (Signing Key)**, tối thiểu 32 ký tự (256 bits) cho HMAC-SHA256. Hỗ trợ alias `Jwt:SigningKey`, `Jwt:Key`. | ⚠️ MỘT PHẦN | `appsettings.json:26` đang chứa **khoá thật dạng plaintext**; file này gitignored nên khoá **không** bị commit, nhưng vẫn là plaintext trên máy dev. `*.example.json` dùng placeholder `<REPLACE_WITH_JWT_SIGNING_KEY_MIN_32_CHARS>`. Production phải cấp qua `Jwt__Signature` |
| `Jwt:ExpirationInMinutes` | `Jwt__ExpirationInMinutes` | `int` | `60` (Prod) / `120` (Dev) | **Thời hạn sống của token** tính bằng phút. Hỗ trợ alias `Jwt:Expiration`. | ✅ CÓ | `appsettings.json`, `appsettings.Development.json` |
| `Jwt:RequireHttpsMetadata` | `Jwt__RequireHttpsMetadata` | `bool` | `false` (Dev) / `true` (Prod) | Bắt buộc HTTPS khi kiểm tra metadata của nhà phát hành. | ✅ CÓ | `appsettings.json` |
> ⚠️ Không service nào phát JWT: `rpc Login` của `UsersApi` **chưa implement** ở User service (GW-03) ⇒ luồng lấy token end-to-end chưa chạy.
---
### B. Cấu Hình Hoạt Động Của Gateway (`Gateway`)
Đặt tại: `appsettings.json`, `appsettings.Development.json`, `gateway.runtime.json`, hoặc biến môi trường `Gateway__*`. Tất cả khoá dưới đây đều là property thật của `GatewayOptions` (`Nexus.Gateway/GatewayOptions.cs:13-40`).
| Khóa cấu hình (JSON) | Biến môi trường tương ứng | Giá trị mẫu | Ý nghĩa & Tác dụng | Trạng thái |
|:---|:---|:---:|:---|:---|
| `Gateway:AuthMode` | `Gateway__AuthMode` | `"Enforce"` (đang dùng) / `"Standard"` / `"Bypass"` | **Chế độ kiểm tra xác thực**:<br>- `"Enforce"` hoặc `"Standard"`: yêu cầu JWT + RBAC nghiêm ngặt (401/403).<br>- `"Bypass"` / `"Mock"` / `"MockBypass"`: **Development-only**, nới lỏng auth cho Mock API.<br>`Gateway:GatewayMode` là **alias legacy**, cấu hình mới phải dùng `AuthMode` (`:38,42,101,110-115`). | ✅ CÓ |
| `Gateway:PermitLimit` | `Gateway__PermitLimit` | `100` | Số request tối đa mặc định trong một cửa sổ (fallback khi policy không khai báo). | ✅ CÓ |
| `Gateway:WindowSeconds` | `Gateway__WindowSeconds` | `60` | Độ dài cửa sổ rate limit mặc định (giây). | ✅ CÓ |
| `Gateway:RateLimits:<policy>:PermitLimit` | `Gateway__RateLimits__<policy>__PermitLimit` | `5` (`auth-strict`), `10` (`auth-login`), `120` (`user-frequent`) | Giới hạn request theo từng policy. Policy thật đang dùng: `auth-strict`, `auth-login`, `user-frequent`, `api`, `default`. | ✅ CÓ |
| `Gateway:RateLimits:<policy>:WindowSeconds` | `Gateway__RateLimits__<policy>__WindowSeconds` | `60` | Độ dài cửa sổ theo giây cho từng policy. | ✅ CÓ |
| `Gateway:ConcurrencyLimit` | `Gateway__ConcurrencyLimit` | `100` | Số request đồng thời tối đa Gateway tiếp nhận. | ✅ CÓ |
| `Gateway:RedisConnectionString` | `Gateway__RedisConnectionString` | `"localhost:6379"` | Địa chỉ Redis cho Distributed Rate Limiting (500MB shared limit). Compose override thành `redis:6379`. | ✅ CÓ |
| `Gateway:RedisKeyPrefix` | `Gateway__RedisKeyPrefix` | `"nexus:gateway"` | Tiền tố khoá Redis. | ✅ CÓ |
| `Gateway:RedisFailOpenInDevelopment` | `Gateway__RedisFailOpenInDevelopment` | `true` (Dev) | Nếu Redis sập và cờ bật: cho request đi qua + log cảnh báo thay vì chặn. Prod mặc định `false`. | ✅ CÓ |
| `Gateway:CorsOrigins` | `Gateway__CorsOrigins__0`, `__1` | `["http://localhost:3000", ...]` | Danh sách URL Frontend được phép gọi API. | ✅ CÓ |
| `Gateway:CorsMethods` | `Gateway__CorsMethods` | `["GET","POST",...]` | Phương thức HTTP được phép qua CORS. | ✅ CÓ |
| `Gateway:CorsHeaders` | `Gateway__CorsHeaders` | `["*"]` | Header được phép gửi lên Gateway. | ✅ CÓ |
| `Gateway:CorsAllowCredentials` | `Gateway__CorsAllowCredentials` | `true` | Cho phép gửi Cookie/Credentials qua CORS. | ✅ CÓ |
| `Gateway:AllowClientModeOverrideInDevelopment` | `Gateway__AllowClientModeOverrideInDevelopment` | `true` (runtime) | Cho phép Client tự đổi mode Mock/Real qua header (vd `X-Service-Mode-catalog: Real`). | ✅ CÓ |
| `Gateway:AllowAnonymousMockAuthInDevelopment` | `Gateway__AllowAnonymousMockAuthInDevelopment` | `true` (runtime) / `false` (base) | Cho phép gọi API không token khi Bypass. | ✅ CÓ |
| `Gateway:AllowMockTokenAuth` | `Gateway__AllowMockTokenAuth` | `true` (runtime) / `false` (base) | Cho phép nhập `admin`/`seller`/`staff`/`buyer` trên Swagger thay token thật khi test Mock. | ✅ CÓ |
| `Gateway:DefaultMockUserId` | `Gateway__DefaultMockUserId` | `"11111111-1111-1111-1111-000000000004"` | UUID user gán cho request mock khi không có token ở Bypass. | ✅ CÓ |
| `Gateway:DefaultMockRole` | `Gateway__DefaultMockRole` | `"buyer"` | Vai trò mặc định gán cho request mock. | ✅ CÓ |
---
### C. Cấu Hình 8 Microservices Upstream (`Gateway:Services:<name>`)
Mỗi service dùng `BackendOptions` (`GatewayOptions.cs:118-125`) với đúng 5 thuộc tính:
| Thuộc tính | Kiểu | Ý nghĩa | Mặc định | Ví dụ | Trạng thái |
|:---|:---:|:---|:---:|:---|:---|
| `Mode` | `string` | **`"Mock"`** (đọc file seed / declarative mock) hoặc **`"Real"`** (gọi gRPC backend thật). | `"Real"` | `"Mock"` | ✅ CÓ |
| `Address` | `string` | Địa chỉ gRPC của service backend. | *(rỗng)* | `"http://localhost:8001"` | ✅ CÓ |
| `TimeoutSeconds` | `int` | Thời gian tối đa chờ backend (quá hạn ⇒ 504). | `5` | `5` | ✅ CÓ |
| `MockDelayMs` | `int` | Giả lập độ trễ mạng khi Mock (ms) để test loading UI. | `0` | `500` | ✅ CÓ |
| `MockStatus` | `int` | Giả lập HTTP status khi Mock để test lỗi mạng. | `200` | `200` / `503` | ✅ CÓ |
#### Danh Sách 8 Microservices Đã Quy Hoạch Chuẩn:
| Service | Address (local) | Chức năng | Mode trong `gateway.runtime.json` | gRPC client ở Gateway |
|:---|:---|:---|:---|:---|
| **`users`** | `http://localhost:8001` | User, Auth, Profile, RBAC | `Mock` | ✅ CÓ (`UsersApiClient`) |
| **`catalog`** | `http://localhost:8002` | Danh mục, Sản phẩm, Tồn kho ảo | `Real` | ❌ CHƯA CÓ |
| **`commerce`** | `http://localhost:8003` | Giỏ hàng, Đơn hàng, Checkout | `Real` | ❌ CHƯA CÓ |
| **`log`** | `http://localhost:8004` | Nhật ký hệ thống tập trung, Event Types | `Real` | ❌ CHƯA CÓ |
| **`auction`** | `http://localhost:8005` | Phòng đấu giá, Đặt giá bid | `Mock` | ❌ CHƯA CÓ |
| **`fulfillment`** | `http://localhost:8006` | Kho bãi vật lý, Phí ship, Vận đơn | `Real` | ❌ CHƯA CÓ |
| **`notification`** | `http://localhost:8007` | Thông báo, Cài đặt thông báo | `Real` | ❌ CHƯA CÓ |
| **`wallet`** | `http://localhost:8008` | Ví điện tử, Số dư, Nạp/rút/chuyển tiền | `Real` | ❌ CHƯA CÓ |
> ⚠️ **Chỉ service `users` có typed client** (`Extensions/GrpcClientsExtension.cs` → `AddNamedGrpcClient<UsersApi.UsersApiClient>("users")`). Đặt `"catalog": { "Mode": "Real" }` hiện **không** làm request đi tới Catalog — sẽ lỗi vì không có `CatalogApi` trong `Contracts/shop.proto` và không có client đăng ký.
---
### D. Cấu Hình Kestrel Server & Observability
| Khóa / Biến môi trường | Vị trí cấu hình | Giá trị | Ý nghĩa | Trạng thái | Bằng chứng |
|:---|:---|:---:|:---|:---|:---|
| `MaxRequestBodySize` | `Program.cs` | `65536` (64KB) | Giới hạn kích thước body, chống tràn RAM. | ✅ CÓ | `Program.cs:9` `o.Limits.MaxRequestBodySize = 64 * 1024` |
| `RequestTimeout` | `CoreServicesExtension.cs` | `65` (giây) | Ngắt request treo quá 65s ⇒ 504. | ✅ CÓ | `Extensions/CoreServicesExtension.cs:93-97` |
| `ASPNETCORE_ENVIRONMENT` | `.env` / `launchSettings.json` | `Development` / `Production` | Môi trường chạy. | ✅ CÓ | `launchSettings.json`, `run-dev.sh` |
| `ASPNETCORE_URLS` | `.env` / `Dockerfile` | `http://+:8080` | Cổng HTTP Gateway lắng nghe (port **8080** — bất biến). | ✅ CÓ | `Dockerfile` (`EXPOSE 8080`, `ENV ASPNETCORE_URLS=http://+:8080`) |
| `OTEL_EXPORTER_OTLP_ENDPOINT`| `.env` / Environment | `http://tempo:4317` (`.env.example`), `http://localhost:4317` (local) | Địa chỉ nhận OTel Traces & Metrics. Alias: `Otel:Endpoint`. **Exporter chỉ bật khi biến này có giá trị** (`Extensions/TelemetryExtension.cs:13,25`). | ⚠️ MỘT PHẦN | Tempo/Grafana **không** có trong `docker-compose.yml` của repo này (compose chỉ có service `gateway`) ⇒ phải chạy stack ngoài |
---
### E. Cấu Hình Giám Sát Sức Khỏe & Tự Hồi Phục (`Gateway:HealthProber`)
Đặt tại: `appsettings.json`, `appsettings.Development.json` hoặc qua biến môi trường `Gateway__HealthProber__*`.
| Khóa cấu hình (JSON) | Biến môi trường tương ứng | Kiểu | Mặc định | Ý nghĩa & Tác dụng | Trạng thái |
|:---|:---|:---:|:---:|:---|:---|
| `Gateway:HealthProber:IntervalSeconds` | `Gateway__HealthProber__IntervalSeconds` | `int` | `60` | Chu kỳ gửi request probe. | ✅ CÓ |
| `Gateway:HealthProber:TimeoutSeconds` | `Gateway__HealthProber__TimeoutSeconds` | `int` | `30` | Thời gian chờ phản hồi `/health` trước khi tính timeout. | ✅ CÓ |
| `Gateway:HealthProber:FailureThreshold` | `Gateway__HealthProber__FailureThreshold` | `int` | `5` | Số lần thất bại liên tiếp trước khi chuyển `Unhealthy` và kích hoạt circuit breaker. | ✅ CÓ |
| `Gateway:HealthProber:StartPeriodSeconds` | `Gateway__HealthProber__StartPeriodSeconds` | `int` | `60` | Ân hạn khi container mới khởi động lại (không tính lỗi trong khoảng này). | ✅ CÓ |
| `Gateway:HealthProber:AutoRecoverContainer` | `Gateway__HealthProber__AutoRecoverContainer` | `bool` | `true` | Bật chuỗi tự hồi phục container khi `Unhealthy`. | ⚠️ MỘT PHẦN — thực thi bằng `docker restart <container>` qua `ProcessStartInfo` (`HealthProber/DockerContainerRecoveryService.cs`); image `mcr.microsoft.com/dotnet/aspnet:8.0` **không có docker CLI** ⇒ không chạy được trong container |
| `Gateway:HealthProber:MaxAutoRecoverAttempts` | `Gateway__HealthProber__MaxAutoRecoverAttempts` | `int` | `3` | Giới hạn số lần hồi phục liên tiếp (chống CrashLoopBackOff). | ✅ CÓ |
| `Gateway:HealthProber:LogDirectory` | `Gateway__HealthProber__LogDirectory` | `string` | `"logs/health"` | Thư mục log sự cố (incident log). | ✅ CÓ |
| `Gateway:HealthProber:MaxLinesPerLogFile` | `Gateway__HealthProber__MaxLinesPerLogFile` | `int` | `1000` | Số dòng tối đa/tệp trước khi xoay sang `incident-YYYYMMDD-XXXX.log`. | ✅ CÓ |
> ⚠️ Trạng thái mặc định của mọi prober là **`Disabled`** (`HealthProber/HealthProberManager.cs:53,85,158`) — cấu hình `IntervalSeconds` **không** tự bật prober; phải gọi `PUT /api/v1/gateway/prober/{service}`.
> ⚠️ **Saga Rollback chưa tồn tại**: `HealthProber/SagaRollbackPublisher.cs` chỉ ghi warning log và trả `Task.CompletedTask`; không có publish RabbitMQ (xem ADR 11).
---
### F. Danh Sách Request Headers Ghi Đè (Header Overrides)
Trong môi trường Development, Gateway hỗ trợ ghi đè cấu hình cho từng request thông qua các HTTP Headers. Tất cả tên dưới đây đã được đối chiếu code:
| Header Name | Giá Trị Hợp Lệ | Mục Đích Sử Dụng | Trạng thái | Bằng chứng |
|:---|:---:|:---|:---|:---|
| `X-Service-Mode-{service}` | `Real` hoặc `Mock` | Ép một service chạy `Real`/`Mock` cho riêng request đó (vd `X-Service-Mode-catalog: Real`). | ✅ CÓ | `Services/GatewayRuntimeManager.cs` |
| `X-Service-Modes` | map (vd `catalog:Real,wallet:Mock`) | Ép nhiều service cùng lúc. | ✅ CÓ | `Services/GatewayRuntimeManager.cs:110` |
| `X-Mock-Mode` | `Mock` hoặc `Real` | Ép toàn bộ service cho riêng request. | ✅ CÓ | `Services/GatewayRuntimeManager.cs:124` |
| `X-Gateway-Auth` | `Bypass` hoặc `Standard`/`Enforce` | Tạm bật/tắt bypass auth cho riêng request đó. | ✅ CÓ | `Services/GatewayRuntimeManager.cs:38` |
| `X-Gateway-Mode-Override` | `Mock`/`Real` | Alias ép mode toàn cục cho request. | ✅ CÓ | `Services/GatewayRuntimeManager.cs:123` |
| `X-Mock-Enforce-Auth` | `true` | Ép kiểm tra auth nghiêm ngặt kể cả khi service đang Mock. | ✅ CÓ | `Security/MockSecurityAdapterMiddleware.cs:38` |
| `X-Require-Real-Auth` | `true` | Bắt buộc JWT thật từ UserService, chặn hoàn toàn mock token. | ✅ CÓ | `Security/MockSecurityAdapterMiddleware.cs:45`, `Security/SecurityConfiguration.cs:71` |
| `X-Mock-Role` | `admin`, `seller`, `staff`, `buyer` | Vai trò giả lập khi gửi mock token. | ✅ CÓ | `Security/MockSecurityAdapterMiddleware.cs:114` |
| `X-Mock-User-Id` | UUID string | UserId giả lập khi gửi mock token. | ✅ CÓ | `Security/MockSecurityAdapterMiddleware.cs:137` |
---
## 4. HƯỚNG DẪN THỰC HÀNH CẤU HÌNH DÀNH CHO DEVELOPER
1. **Khi chạy local để code giao diện hoặc test độc lập với Mock**:
   - Copy `Nexus.Gateway/appsettings.example.json` → `Nexus.Gateway/appsettings.json` và `appsettings.Development.example.json` → `appsettings.Development.json` (hai file thật **không** có trong git), rồi điền `Jwt:Signature` tối thiểu 32 ký tự.
   - Ở `appsettings.Development.json`, toàn bộ 8 services đã bật `"Mode": "Mock"`. Chạy `./run-dev.sh` (hoặc `dotnet run --project Nexus.Gateway`) ⇒ `http://localhost:8080`.
   - Nếu muốn test không cần đăng nhập: mở `gateway.runtime.json`, đổi `"AuthMode": "Bypass"` (giá trị đang commit là `"Enforce"`).
   - ⚠️ Chỉ **route có action hoặc có mock definition** mới trả dữ liệu; 11 controller stub (vd `catalog/products`, `commerce/orders`) trả **404** kể cả khi `Mode: "Mock"`.
2. **Khi code xong 1 microservice (ví dụ Catalog Service)**:
   - Khởi động Catalog gRPC ở `http://localhost:8002`.
   - ⚠️ **Trước hết phải thêm `CatalogApi` vào `Nexus.Gateway/Contracts/shop.proto` + đăng ký client** trong `Extensions/GrpcClientsExtension.cs` + viết action trong `ProductsController` (hiện là stub). Nếu không, đổi mode chỉ gây lỗi.
   - Sau đó sửa `gateway.runtime.json`:
     ```json
     "catalog": {
       "Mode": "Real",
       "Address": "http://localhost:8002"
     }
     ```
   - **Lưu file là có hiệu lực ngay** — `gateway.runtime.json` nạp với `reloadOnChange: true` (`Extensions/ConfigurationExtensions.cs:15`), **không cần restart**.
   - 7 service còn lại vẫn chạy Mock bình thường; nhưng các service đó cũng **chưa có client** nên chỉ Mock mới hoạt động.
3. **Khi triển khai Docker / Production**:
   - Tạo `.env` từ `.env.example` (`GATEWAY_PORT=8080`, `REDIS_CONNECTION_STRING`, `OTEL_EXPORTER_OTLP_ENDPOINT`, 8 `*_SERVICE_URL`).
   - Cấp `Jwt__Signature` thật dài ≥ 32 ký tự qua biến môi trường / secret store — **không** dựa vào `appsettings.json`.
   - Đặt `ASPNETCORE_ENVIRONMENT=Production`.
   - Toàn bộ 8 services trong `appsettings.json` mặc định `Mode: "Real"`, `AuthMode: "Enforce"`; mọi cờ bypass đều `false` và options validation sẽ **fail-fast** nếu ai đó bật Mock/Bypass ở Production.
   - ⚠️ Health prober mặc định `Disabled`; container tự hồi phục cần docker CLI (không có trong image) ⇒ đừng tin `AutoRecoverContainer: true` là đã hoạt động.