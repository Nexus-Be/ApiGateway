# 04 — MOCK ENGINE & SEED DATA SPECIFICATION
> **Trạng thái triển khai (2026-10-24):** ⚠️ Một phần — 9/9 file seed thật chỉ phục vụ `auth` (4) + `users` (5); 6/6 file declarative mock phục vụ 6 route; 3 thư mục seed nghiệp vụ còn lại mới có `README.md`.
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-24.
---
## 1. HAI CƠ CHẾ MOCK SONG SONG (ĐỀU ĐANG CHẠY)
| Cơ chế | Cách hoạt động | Có dữ liệu thật? | Trạng thái | Bằng chứng |
|:---|:---|:---|:---|:---|
| **Fixture theo Controller Action** | Controller gọi `_dispatcher.DispatchAsync(...)`; `MockDataMapper` đọc `Seeds/{service}/{operation}.json` rồi parse bằng `MessageParser<T>.ParseJson()` | 9 file (`auth` 4, `users` 5) | ✅ CÓ | `Dispatching/MockDataMapper.cs:27`; `Nexus.Gateway/Seeds/` |
| **Declarative mock routes** | `MockEngineMiddleware` khớp `Mocks/*.mock.json` **khi không có endpoint `ControllerActionDescriptor` nào khớp** và effective mode của service là `Mock` | 6 file | ✅ CÓ | `MockEngine/MockEngineMiddleware.cs:30-51`; `Nexus.Gateway/Mocks/` |
Tài liệu cũ khẳng định "declarative `*.mock.json` không được dùng, chỉ Controller + Seeds mới hợp lệ" là **sai**: cả hai cơ chế đều đang chạy. Declarative mock chỉ phục vụ route **chưa có controller action**; ngay khi service chuyển `Real`, route không có controller tương ứng trả **404** (không tự forward).
---
## 2. CẤU TRÚC SEED FIXTURE THẬT ĐANG CÓ
```
Nexus.Gateway/Seeds/
├── README.md
├── auth/           ✅ 4 file: login.json, register.json, verify-otp.json, resend-otp.json
├── users/          ✅ 5 file: login.json, profile.json, register.json, verify-otp.json, resend-otp.json
├── cart/           ❌ chỉ có README.md — đặc tả mục tiêu, chưa triển khai
├── orders/         ❌ chỉ có README.md — đặc tả mục tiêu, chưa triển khai
└── products/       ❌ chỉ có README.md — đặc tả mục tiêu, chưa triển khai
```
Không có thư mục seed cho `catalog`, `commerce`, `log`, `auction`, `fulfillment`, `notification`, `wallet` — các tên file kiểu `Seeds/catalog/products.json` từng được mô tả trong tài liệu cũ **chưa từng tồn tại**.
### 2.1 Quy ước đặt tên file seed
- Đường dẫn: `Seeds/{service}/{operation}.json`, trong đó `{service}` là tên service trong `Gateway:Services` (`users`, `auth`, `catalog`, `commerce`, `log`, `auction`, `fulfillment`, `notification`, `wallet`) và `{operation}` là tên thao tác lowercase, có gạch nối (`login`, `register`, `verify-otp`, `resend-otp`, `profile`).
- Nội dung file là **JSON của message Protobuf tương ứng**, phải parse được bằng `MessageParser<T>.ParseJson()` — sai kiểu/thiếu trường bắt buộc sẽ lỗi ngay khi request.
- Không lưu dữ liệu động/state trong seed: seed là dữ liệu tĩnh.
### 2.2 Ví dụ seed hiện có
| File | Phục vụ route | Trạng thái | Bằng chứng |
|:---|:---|:---|:---|
| `Seeds/auth/login.json` | `POST /api/v1/auth/login` | ✅ CÓ | `Controllers/AuthController.cs:106` |
| `Seeds/auth/register.json` | `POST /api/v1/auth/register` | ✅ CÓ | `Controllers/AuthController.cs:29` |
| `Seeds/auth/verify-otp.json` | `POST /api/v1/auth/verify-otp` | ✅ CÓ | `Controllers/AuthController.cs:61` |
| `Seeds/auth/resend-otp.json` | `POST /api/v1/auth/resend-otp` | ✅ CÓ | `Controllers/AuthController.cs:84` |
| `Seeds/users/profile.json` | `GET /api/v1/users/me` | ✅ CÓ | `Controllers/UsersController.cs:27` |
| `Seeds/users/{login,register,verify-otp,resend-otp}.json` | Đường dự phòng khi `auth` không có file tương ứng | ✅ CÓ | `Dispatching/MockDataMapper.cs` |
---
## 3. DECLARATIVE MOCK DEFINITIONS (`Nexus.Gateway/Mocks/`)
6 file định nghĩa + `README.md`:
| File | Route | Service | Trạng thái | Bằng chứng |
|:---|:---|:---|:---|:---|
| `auth-login.mock.json` | `POST /api/v1/auth/login` | `users` | ✅ CÓ | `Mocks/auth-login.mock.json` |
| `auth-register.mock.json` | `POST /api/v1/auth/register` | `users` | ✅ CÓ | `Mocks/auth-register.mock.json` |
| `auth-verify-otp.mock.json` | `POST /api/v1/auth/verify-otp` | `users` | ✅ CÓ | `Mocks/auth-verify-otp.mock.json` |
| `auth-resend-otp.mock.json` | `POST /api/v1/auth/resend-otp` | `users` | ✅ CÓ | `Mocks/auth-resend-otp.mock.json` |
| `users-me.mock.json` | `GET /api/v1/users/me` | `users` | ✅ CÓ | `Mocks/users-me.mock.json` |
| `catalog-products.mock.json` | `GET /api/v1/catalog/products` | `catalog` | ✅ CÓ (route controller là stub ❌) | `Mocks/catalog-products.mock.json`; `Controllers/ProductsController.cs` |
Các definition cho `cart`, `orders`, `wallet`, `auction`, `fulfillment`, `notification`, `log`: ❌ CHƯA CÓ — đặc tả mục tiêu, chưa triển khai.
Hot reload definitions: `POST /api/v1/gateway/mock-engine/reload` (✅ CÓ, `Controllers/GatewayControlController.cs:120`); xem danh sách đang nạp: `GET /api/v1/gateway/mock-engine/definitions` (`:101`).
---
## 4. NGUYÊN TẮC KHI THÊM DỮ LIỆU MOCK
1. Ưu tiên **Controller Action + Seed fixture** khi route cần envelope chuẩn (`success/statusCode/data/traceId/timestamp`) hoặc cần phân trang thật; dùng **declarative mock** khi chỉ cần trả body tĩnh cho route chưa có action.
2. Không viết state machine/in-memory DB trong Gateway: seed là dữ liệu tĩnh, mọi biến đổi nghiệp vụ thuộc backend.
3. Mọi file seed phải parse được bằng Protobuf JSON parser tương ứng; không thêm trường không có trong `.proto`.
4. Không sửa `Nexus.Gateway/Contracts/shop.proto` để "vừa khớp" seed — hợp đồng là nguồn sự thật.