# 03 — BẢO MẬT & TRẠNG THÁI RUNTIME CỦA GATEWAY (SECURITY & RUNTIME MODES)
> **Trạng thái triển khai (2026-10-27):** ✅ Hoàn tất — hai trục `AuthMode` và `Services:*:Mode` hoạt động độc lập và hoàn chỉnh; 15/15 Controllers hỗ trợ cả chế độ Mock (nạp JSON fixture trong `Seeds/`) và Real (gọi REST downstream qua `HttpBackendCaller`).
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-27.
Tên khóa cấu hình thật là **`Gateway:AuthMode`** (property `GatewayOptions.AuthMode`, `Nexus.Gateway/GatewayOptions.cs:38`). `GatewayOptions.GatewayMode` (`GatewayOptions.cs:42`) chỉ là **alias legacy** được đọc khi `AuthMode` không có mặt (`GatewayOptions.cs:101`: `AuthMode ?? GatewayMode ?? "Enforce"`); tài liệu cũ gọi trục này là "GatewayMode" là cách gọi sai đã lỗi thời.
---
## 1. HAI TRỤC ĐỘC LẬP: `AuthMode` VÀ `Services:{name}:Mode`
| Trục | Khóa cấu hình | Giá trị hợp lệ | Mặc định | Trạng thái | Bằng chứng |
|:---|:---|:---|:---|:---|:---|
| Auth mode | `Gateway:AuthMode` | `Enforce`, `Standard` (đồng nghĩa enforce); `Bypass`, `Mock`, `MockBypass` (**chỉ Development**) | `Enforce` | ✅ CÓ | `Nexus.Gateway/GatewayOptions.cs:38,101,110-115` |
| Service mode | `Gateway:Services:{name}:Mode` | `Real`, `Mock` | `Real` | ✅ CÓ | `Nexus.Gateway/BackendOptions.cs` (`Mode = "Real"`) |
`AuthMode` và `Mode` của từng service là **hai trục khác nhau**: `AuthMode` quyết định có kiểm tra JWT/RBAC hay không; `Services:{name}:Mode` quyết định request được phục vụ bằng fixture JSON hay gọi REST downstream thật.
Cả hai đều được validate lúc khởi động: `AddOptions<GatewayOptions>().Bind(...).Validate(x => x.IsValid(isDevelopment), "Invalid gateway options; Mock and plaintext upstreams are Development-only.").ValidateOnStart()` (`Nexus.Gateway/Extensions/ConfigurationExtensions.cs`). Nghĩa là `Bypass`/`Mock` auth mode và upstream `http://` **chỉ hợp lệ khi `ASPNETCORE_ENVIRONMENT=Development`**; production vi phạm sẽ fail-fast ngay khi start.
```
                       ┌──────────────────────────────┐
                       │  Client Request Đến Gateway  │
                       └──────────────┬───────────────┘
                                      │
                       [Đọc Gateway:AuthMode (mặc định "Enforce")]
                                      │
                   ┌──────────────────┴──────────────────┐
                   ▼                                     ▼
      [ AuthMode: "Enforce" / "Standard" ]   [ AuthMode: "Bypass" (chỉ Development) ]
                   │                                     │
       Bắt buộc JWT hợp lệ trên header         Với request tới service đang Mock:
       Authorization: Bearer.                  bỏ qua kiểm tra auth, gán danh tính
       Không token -> 401 Unauthorized.        giả lập (DefaultMockUserId/DefaultMockRole)
       Sai quyền -> 403 Forbidden.             ────────────────────────────────────────
                                               Ngoại lệ: controller gắn
                                               [DisableMockAuthorizationBypass]
                                               (/gateway/..., /mock-engine/...,
                                               /gateway/prober/...) vẫn bắt buộc
                                               JWT Gateway admin thật.
```
1. **`"Enforce"` / `"Standard"`** (mặc định, giá trị đang dùng trong repo):
   - Mọi endpoint yêu cầu đăng nhập phải mang JWT hợp lệ. Ứng dụng hiện **chưa có service nào phát JWT**: `UsersApi.Login` có trong `Nexus.Gateway/Contracts/shop.proto` nhưng phía User service chưa implement.
   - Token không đủ quyền (ví dụ role `buyer` gọi API quản trị) $\rightarrow$ `403 Forbidden`.
2. **`"Bypass"`** (chỉ Development): bỏ qua kiểm tra auth cho request tới service đang ở `Mock`, tự gán `ClaimsPrincipal` giả lập. **Chặn bypass tuyệt đối**: `[DisableMockAuthorizationBypass]` trên `GatewayControlController` và `HealthProberController`, cộng thêm `[Authorize(GatewayPolicies.GatewayAdmin)]` — hai controller này luôn yêu cầu JWT admin thật.
3. **Các cờ bypass liên quan** (đều chỉ có tác dụng ở Development, validate bởi `IsValid`):
| Cờ | Ý nghĩa | Giá trị trong `gateway.runtime.json` | Trạng thái | Bằng chứng |
|:---|:---|:---|:---|:---|
| `AllowClientModeOverrideInDevelopment` | Cho client ép mode bằng header `X-Service-Mode-{service}` | `true` | ✅ CÓ | `GatewayOptions.cs`; `Dispatching/ModeEvaluator.cs` |
| `AllowAnonymousMockAuthInDevelopment` | Cho phép request Mock không token khi `AuthMode=Bypass` | `true` | ✅ CÓ | `Security/` (`UseMockSecurityAdapter`) |
| `AllowMockTokenAuth` | Chấp nhận token mock tổng hợp | `true` | ✅ CÓ | `Security/SecurityConfiguration.cs` |
| `RedisFailOpenInDevelopment` | Redis chết thì cho qua thay vì chặn | `true` | ✅ CÓ | `Middleware/RedisRateLimitMiddleware.cs` |
| `DefaultMockUserId` / `DefaultMockRole` | Danh tính gán khi bypass | `11111111-1111-1111-1111-000000000004` / `buyer` | ✅ CÓ | `gateway.runtime.json` |
---
## 2. CHUYỂN ĐỔI CHẾ ĐỘ MOCK VÀ REAL (SERVICE-LEVEL SWITCHING)
Gateway hỗ trợ chuyển đổi từng service độc lập mà không cần restart server.
1. **Hot-Reload Qua File `gateway.runtime.json`** — file này **được track trong git** và nạp **sau** `appsettings.{Environment}.json`, trước biến môi trường (`Extensions/ConfigurationExtensions.cs`), có `reloadOnChange`. Nội dung thật đang nằm trong repo (`users`+`auction` = `Mock`, 6 service còn lại = `Real`, `AuthMode: "Enforce"`):
   ```json
   {
     "Gateway": {
       "AuthMode": "Enforce",
       "AllowClientModeOverrideInDevelopment": true,
       "AllowAnonymousMockAuthInDevelopment": true,
       "AllowMockTokenAuth": true,
       "RedisFailOpenInDevelopment": true,
       "DefaultMockUserId": "11111111-1111-1111-1111-000000000004",
       "DefaultMockRole": "buyer",
       "Services": {
         "users": { "Mode": "Mock" },
         "auction": { "Mode": "Mock" },
         "catalog": { "Mode": "Real", "Address": "http://localhost:8002" }
       }
     }
   }
   ```
   ⚠️ MỘT PHẦN: cơ chế reload có, nhưng nên xác minh mode có hiệu lực bằng `GET /api/v1/gateway/modes` thay vì giả định hot reload tức thì.
2. **Chuyển Đổi Qua API Runtime** (yêu cầu JWT Gateway admin):
   ```http
   PUT /api/v1/gateway/modes/catalog
   Content-Type: application/json
   Authorization: Bearer <gateway-admin-token>
   { "mode": "Real" }
   ```
   Trạng thái: ✅ CÓ — `Controllers/GatewayControlController.cs:58` (`PUT gateway/modes/{service}`), cùng `PUT /api/v1/gateway/mode` (`:42`) cho `AuthMode` và `PUT /api/v1/gateway/modes` (`:74`) cho bulk.
3. **Override Cho Từng Request Qua Header (Development)**:
   - `X-Service-Mode-{service}: Real` $\rightarrow$ ép request đó gọi backend thật (chỉ khi `AllowClientModeOverrideInDevelopment=true`).
   - `X-Mock-Enforce-Auth: true` $\rightarrow$ buộc kiểm tra auth nghiêm ngặt trên endpoint mock.
---
## 3. GIỚI HẠN TẦN SUẤT GỌI PHÂN TÁN (DISTRIBUTED RATE LIMITING)
| Hạng mục | Trạng thái | Bằng chứng |
|:---|:---|:---|
| Middleware Redis phân tán | ✅ CÓ | `Nexus.Gateway/Middleware/RedisRateLimitMiddleware.cs` |
| Policy ASP.NET Core `"api"` (controller dùng `[EnableRateLimiting("api")]`) | ✅ CÓ | `Security/EdgeMiddlewareConfiguration.cs:87-106` |
| Policy RBAC (`authenticated`, `customer`, `seller`, `staff`, `admin`, `gateway-admin`) | ✅ CÓ | `Security/SecurityConfiguration.cs:156-161` (`GatewayPolicies`) |
- Phân vùng theo IP Client và UserId (nếu đã đăng nhập).
- Thuật toán: Fixed/Sliding Window dựa trên Redis atomic commands.
- Mã lỗi khi vượt ngưỡng: `429 Too Many Requests` kèm header `Retry-After: <seconds>`.
- Khi Redis không sẵn sàng: Development + `RedisFailOpenInDevelopment=true` $\rightarrow$ fail-open (cho qua); production $\rightarrow$ fail-closed.
---
## 4. BÍ MẬT: `Jwt:Signature` NẰM Ở ĐÂU
| Hạng mục | Trạng thái | Bằng chứng |
|:---|:---|:---|
| `appsettings.json` / `appsettings.Development.json` được commit | ❌ CHƯA CÓ | `.gitignore:81` = `appsettings*.json` + `!appsettings*.example.json`; `git check-ignore` xác nhận hai file này bị ignore |
| Khóa thật trong file bản mẫu | ❌ CHƯA CÓ | `appsettings.example.json` dùng `<REPLACE_WITH_JWT_SIGNING_KEY_MIN_32_CHARS>`; `appsettings.Development.example.json` dùng `<REPLACE_WITH_DEV_JWT_SIGNING_KEY_MIN_32_CHARS>` |
| Khóa hardcode trong working tree (file bị ignore) | ✅ CÓ (rủi ro vận hành) | `Nexus.Gateway/appsettings.json` — `Jwt:Signature` là chuỗi literal; `git grep 'd)aam<'` = 0 hit nên **chưa từng bị commit** |
Khuyến nghị: nạp `Jwt__Signature` qua biến môi trường/secret store khi deploy, không commit khóa thật.

---

## 5. CHUYỂN TIẾP NGỮ CẢNH ĐỊNH DANH DOWNSTREAM (IDENTITY & PERMISSION PROPAGATION)

| Header Chuyển Tiếp | Nguồn Dữ Liệu | Mục Đích Nghiệp Vụ | Trạng Thái |
|:---|:---|:---|:---:|
| `Authorization` | Header gốc `Authorization: Bearer <token>` | Giữ nguyên vẹn token cho downstream nếu cần kiểm tra sâu | ✅ CÓ |
| `x-trace-id` | Header `x-trace-id` hoặc sinh mới qua `Activity.Current?.Id` | Mã vết phân tán phục vụ truy vết OpenTelemetry | ✅ CÓ |
| `x-user-id` | Claim `sub`, `id`, hoặc `NameIdentifier` | ID định danh người dùng (UUID) | ✅ CÓ |
| `x-user-roles` | Toàn bộ claims `role` phân cách bằng dấu phẩy | Danh sách vai trò (`buyer`, `seller`, `admin`) | ✅ CÓ |
| `x-user-email` | Claim `email` | Địa chỉ email người dùng | ✅ CÓ |
| `x-user-permissions` | Toàn bộ claims `permission` và `permissions` phân cách dấu phẩy | Danh sách mã quyền hạn chi tiết phục vụ PBAC | ✅ CÓ |

Triển khai tại `Nexus.Gateway/Dispatching/HttpBackendCaller.cs:240-305` (`ForwardContextHeaders`). Downstream microservices kiểm tra quyền trực tiếp trên header `x-user-permissions` in-memory mà không cần truy vấn ngược database.
