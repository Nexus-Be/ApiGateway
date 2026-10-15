# Cẩm Nang Cấu Hình & Biến Môi Trường Nexus API Gateway (CONFIG_REFERENCE.md)

Tài liệu này giải thích chi tiết ý nghĩa của **từng file cấu hình**, **từng biến môi trường**, **nhiệm vụ của chúng** và **thứ tự ưu tiên khi nạp cấu hình (Configuration Precedence)** trong Nexus API Gateway (.NET 8).

---

## 1. Bản Đồ Các File Cấu Hình Trong Dự Án

| File | Vị trí | Mục đích sử dụng | Được commit lên Git? |
|:---|:---|:---|:---:|
| `appsettings.json` | `Nexus.Gateway/` | Cấu hình cơ sở (Base/Production). Chứa các giá trị mặc định cho toàn hệ thống. | ✅ Có |
| `appsettings.Development.json` | `Nexus.Gateway/` | Cấu hình cho môi trường phát triển cục bộ (Local Development). Ghi đè các service sang chế độ Mock. | ✅ Có |
| `appsettings.example.json` | `Nexus.Gateway/` | File mẫu tham khảo cấu hình cơ sở cho team. | ✅ Có |
| `gateway.runtime.json` | `Nexus.Gateway/` | File runtime cấu hình động (hỗ trợ hot-reload khi bật/tắt Mock qua API). | ✅ Có |
| `gateway.runtime.example.json`| `Nexus.Gateway/` | File mẫu runtime cấu hình động. | ✅ Có |
| `launchSettings.json` | `Nexus.Gateway/Properties/` | Cấu hình cổng chạy khi khởi động local qua Visual Studio, Rider hoặc `dotnet run`. | ✅ Có |
| `.env.example` | Thư mục gốc (`ApiGateway/`) | File mẫu danh sách các biến môi trường (Environment Variables) cho server/Docker. | ✅ Có |
| `.env` | Thư mục gốc (`ApiGateway/`) | File chứa biến môi trường thực tế và bí mật (Secret). | ❌ Không (đã chặn trong `.gitignore`) |

---

## 2. Thứ Tự Ưu Tiên Nạp Cấu Hình (Configuration Precedence)

Trong .NET 8, Gateway nạp cấu hình theo thứ tự sau (file/nguồn nạp sau sẽ **ghi đè** giá trị của nguồn nạp trước nếu trùng key):

```
1. appsettings.json (Cơ sở)
       ↓ ghi đè bởi
2. appsettings.{Environment}.json (Ví dụ: appsettings.Development.json)
       ↓ ghi đè bởi
3. gateway.runtime.json (Cấu hình runtime hot-reload, tự động cập nhật khi đổi mode)
       ↓ ghi đè bởi
4. Environment Variables (Biến môi trường hệ thống / Docker / file .env)
       ↓ ghi đè bởi
5. Request HTTP Headers (X-Service-Mode-xxx, X-Gateway-Auth — ưu tiên cao nhất từng request)
```

---

## 3. Chi Tiết Từng Phần Cấu Hình & Biến Môi Trường

### A. Cấu Hình Xác Thực JWT (`Jwt`)
Đặt tại: `appsettings.json`, `appsettings.Development.json`, hoặc qua biến môi trường `Jwt__*`.

| Khóa cấu hình (JSON) | Biến môi trường tương ứng | Kiểu | Ý nghĩa & Tác dụng |
|:---|:---|:---:|:---|
| `Jwt:Issuer` | `Jwt__Issuer` | `string` | **Nguồn phát hành token**. URL của Identity/Users Service cấp phát token (mặc định: `http://localhost:8001`). Gateway dùng để kiểm tra claim `iss`. |
| `Jwt:Authority` | `Jwt__Authority` | `string` | **Địa chỉ máy chủ xác thực OIDC**. Tương đương với Issuer khi chạy cục bộ. |
| `Jwt:Audience` | `Jwt__Audience` | `string` | **Đối tượng nhận token**. Mã định danh API mà token này hướng tới (mặc định: `shoppingcart-api`). Gateway kiểm tra claim `aud`. |
| `Jwt:Signature` | `Jwt__Signature` | `string` | **Khóa bí mật ký đối xứng (Signing Key)**. Chuỗi bảo mật tối thiểu 32 ký tự (256 bits) dùng cho thuật toán HMAC-SHA256 để xác thực chữ ký token. |
| `Jwt:ExpirationInMinutes` | `Jwt__ExpirationInMinutes` | `int` | **Thời hạn sống của token** tính bằng phút. Mặc định `60` phút ở Production, `120` phút ở Development. |
| `Jwt:RequireHttpsMetadata` | `Jwt__RequireHttpsMetadata` | `bool` | Bắt buộc HTTPS khi kiểm tra metadata của nhà phát hành. Mặc định `false` khi chạy local dev, `true` ở Production. |

---

### B. Cấu Hình Hoạt Động Của Gateway (`Gateway`)
Đặt tại: `appsettings.json`, `appsettings.Development.json`, `gateway.runtime.json`, hoặc biến môi trường `Gateway__*`.

| Khóa cấu hình (JSON) | Biến môi trường tương ứng | Giá trị mẫu | Ý nghĩa & Tác dụng |
|:---|:---|:---:|:---|
| `Gateway:AuthMode` | `Gateway__AuthMode` | `"Enforce"` hoặc `"Bypass"` | **Chế độ kiểm tra xác thực của Gateway**:<br>- `Enforce`: Yêu cầu JWT và kiểm tra quyền RBAC nghiêm ngặt.<br>- `Bypass`: Tự động nới lỏng auth để Frontend test độc lập với Mock API. |
| `Gateway:PermitLimit` | `Gateway__PermitLimit` | `100` | Số lượng request tối đa được phép trong một cửa sổ thời gian (Sliding Window Rate Limit). |
| `Gateway:WindowSeconds` | `Gateway__WindowSeconds` | `60` | Thời gian của cửa sổ rate limit (tính bằng giây). Ví dụ `100` request trong `60` giây. |
| `Gateway:ConcurrencyLimit` | `Gateway__ConcurrencyLimit` | `100` | Số lượng request đồng thời tối đa Gateway tiếp nhận cùng một thời điểm. |
| `Gateway:RedisConnectionString` | `Gateway__RedisConnectionString` | `"localhost:6379"` | Địa chỉ kết nối Redis phục vụ tính năng Distributed Rate Limiting. |
| `Gateway:RedisKeyPrefix` | `Gateway__RedisKeyPrefix` | `"nexus:gateway"` | Tiền tố cho các khóa (keys) lưu trong Redis để không trùng với ứng dụng khác. |
| `Gateway:RedisFailOpenInDevelopment` | `Gateway__RedisFailOpenInDevelopment` | `true` / `false` | Nếu bật `true` ở Dev: Nếu Redis bị sập hoặc chưa bật, Gateway tự động cho request đi qua và log cảnh báo, không làm gián đoạn việc code. |
| `Gateway:CorsOrigins` | `Gateway__CorsOrigins__0`, `__1` | `["http://localhost:3000", ...]` | Danh sách URL Frontend được phép gọi API (tránh lỗi CORS Policy của trình duyệt). |
| `Gateway:CorsMethods` | `Gateway__CorsMethods` | `["GET", "POST", ...]` | Các phương thức HTTP được phép qua CORS. |
| `Gateway:CorsHeaders` | `Gateway__CorsHeaders` | `["*"]` | Các HTTP Header được phép gửi lên Gateway. |
| `Gateway:CorsAllowCredentials` | `Gateway__CorsAllowCredentials` | `true` | Cho phép gửi Cookie/Credentials qua CORS. |
| `Gateway:AllowClientModeOverrideInDevelopment` | `Gateway__AllowClientModeOverrideInDevelopment` | `true` | Cho phép Client tự đổi mode Mock/Real qua Header (ví dụ: `X-Service-Mode-catalog: Real`). |
| `Gateway:AllowAnonymousMockAuthInDevelopment` | `Gateway__AllowAnonymousMockAuthInDevelopment` | `true` | Cho phép gọi API không cần token khi Gateway ở chế độ Bypass. |
| `Gateway:AllowMockTokenAuth` | `Gateway__AllowMockTokenAuth` | `true` | Cho phép nhập các từ khóa `admin`, `seller`, `buyer` trên Swagger thay cho token JWT thật khi test Mock. |
| `Gateway:DefaultMockUserId` | `Gateway__DefaultMockUserId` | `"11111111-1111-1111-1111-000000000004"` | Mã UUID user mặc định gán cho các request mock khi không có token. |
| `Gateway:DefaultMockRole` | `Gateway__DefaultMockRole` | `"buyer"` | Quyền mặc định gán cho request mock. |

---

### C. Cấu Hình 8 Microservices Upstream (`Gateway:Services:<name>`)
Mỗi service có 5 thuộc tính cấu hình:

| Thuộc tính | Kiểu | Ý nghĩa | Ví dụ |
|:---|:---:|:---|:---|
| `Mode` | `string` | **`"Mock"`** (đọc file mock cục bộ) hoặc **`"Real"`** (gọi gRPC sang backend thật). | `"Mock"` |
| `Address` | `string` | Địa chỉ gRPC của service backend thật. | `"http://localhost:8001"` |
| `TimeoutSeconds` | `int` | Thời gian tối đa (timeout) chờ backend phản hồi trước khi trả về lỗi 504 Gateway Timeout. | `5` (giây) |
| `MockDelayMs` | `int` | Giả lập độ trễ mạng khi ở chế độ Mock (tính bằng mili-giây). | `0` (hoặc `500` để test loading) |
| `MockStatus` | `int` | Giả lập HTTP status code trả về khi ở Mock (dùng để test tình huống lỗi mạng). | `200` (hoặc `503` để test lỗi) |

#### Danh Sách 8 Services Đã Quy Hoạch Chuẩn:
- **`users`**: `http://localhost:8001` (User, Auth, RBAC)
- **`catalog`**: `http://localhost:8002` (Danh mục, Sản phẩm, Tồn kho ảo)
- **`commerce`**: `http://localhost:8003` (Giỏ hàng, Đơn hàng)
- **`audit`**: `http://localhost:8004` (Nhật ký kiểm toán)
- **`auction`**: `http://localhost:8005` (Đấu giá trực tuyến)
- **`fulfillment`**: `http://localhost:8006` (Kho bãi, Giao vận)
- **`notification`**: `http://localhost:8007` (Thông báo)
- **`wallet`**: `http://localhost:8008` (Ví điện tử, Số dư, Nạp/rút/chuyển tiền)

---

### D. Cấu Hình Server & Quan Sát (Observability / OpenTelemetry)

| Biến môi trường | File cấu hình | Giá trị | Ý nghĩa |
|:---|:---|:---:|:---|
| `ASPNETCORE_ENVIRONMENT` | `.env` / `launchSettings.json` | `Development` / `Production` | Xác định môi trường chạy của Gateway. |
| `ASPNETCORE_URLS` | `.env` / `Dockerfile` | `http://+:8000` | Cổng HTTP mà Gateway lắng nghe (Port `8000`). |
| `OTEL_EXPORTER_OTLP_ENDPOINT`| `.env` (Tùy chọn) | `http://localhost:4317` | Địa chỉ nhận dữ liệu OpenTelemetry Traces & Metrics (Jaeger / OTLP Collector). |

---

## 4. Cách Sử Dụng Cho Developer Hàng Ngày

1. **Khi chạy local để code giao diện hoặc test mock**:
   - Sử dụng file `appsettings.Development.json`.
   - Toàn bộ 8 services đã được bật sẵn `"Mode": "Mock"`. Bạn chỉ việc `dotnet run --project Nexus.Gateway` là toàn bộ API đều chạy trơn tru.
2. **Khi code xong 1 microservice (ví dụ Wallet Service)**:
   - Khởi động `Wallet Service` thật ở cổng `http://localhost:8008`.
   - Mở `appsettings.Development.json`, sửa `"wallet": { "Mode": "Real" }`.
   - Gateway sẽ tự động chuyển tiếp request tới `http://localhost:8008` qua gRPC, trong khi 7 service còn lại vẫn chạy Mock bình thường!
3. **Khi triển khai Docker / Production**:
   - Tạo file `.env` từ `.env.example`.
   - Điền khóa bí mật `Jwt__Signature` thật dài tối thiểu 32 ký tự.
   - Đặt `ASPNETCORE_ENVIRONMENT=Production`.
