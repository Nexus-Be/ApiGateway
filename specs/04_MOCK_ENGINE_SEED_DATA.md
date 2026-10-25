# 04 — MOCK ENGINE & SEED DATA SPECIFICATION
> **Trạng thái triển khai (2026-10-25):** ⚠️ Một phần — 25 file seed thật: `auth` (4), `users` (5), `auction` (16, phủ đủ 16 route AUC); 6/6 file declarative mock phục vụ 6 route; `cart`/`orders`/`products` mới có `README.md`.
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-25.
---
## 1. HAI CƠ CHẾ MOCK SONG SONG (ĐỀU ĐANG CHẠY)
| Cơ chế | Cách hoạt động | Có dữ liệu thật? | Trạng thái | Bằng chứng |
|:---|:---|:---|:---|:---|
| **Fixture theo Controller Action** | Controller gọi `_dispatcher.DispatchAsync(...)`; `MockDataMapper` đọc `Seeds/{service}/{operation}.json` rồi parse bằng `MessageParser<T>.ParseJson()` | 25 file (`auth` 4, `users` 5, `auction` 16) | ✅ CÓ | `Dispatching/MockDataMapper.cs:27`; `Nexus.Gateway/Seeds/` |
| **Declarative mock routes** | `MockEngineMiddleware` khớp `Mocks/*.mock.json` **khi không có endpoint `ControllerActionDescriptor` nào khớp** và effective mode của service là `Mock` | 6 file | ✅ CÓ | `MockEngine/MockEngineMiddleware.cs:30-51`; `Nexus.Gateway/Mocks/` |
Tài liệu cũ khẳng định "declarative `*.mock.json` không được dùng, chỉ Controller + Seeds mới hợp lệ" là **sai**: cả hai cơ chế đều đang chạy. Declarative mock chỉ phục vụ route **chưa có controller action**; ngay khi service chuyển `Real`, route không có controller tương ứng trả **404** (không tự forward).
---
## 2. CẤU TRÚC SEED FIXTURE THẬT ĐANG CÓ
```
Nexus.Gateway/Seeds/
├── README.md
├── auth/           ✅ 4 file: login.json, register.json, verify-otp.json, resend-otp.json
├── users/          ✅ 5 file: login.json, profile.json, register.json, verify-otp.json, resend-otp.json
├── auction/        ✅ 16 file: auctions, auction, auction-bids, auction-bid-place, me-bids, auction-result,
│                      auction-create, auction-update, auction-cancel, auction-recreate, seller-auctions,
│                      auction-status-history, auction-force-cancel, auction-saga, auction-configs, auction-config-update
├── cart/           ❌ chỉ có README.md — đặc tả mục tiêu, chưa triển khai
├── orders/         ❌ chỉ có README.md — đặc tả mục tiêu, chưa triển khai
└── products/       ❌ chỉ có README.md — đặc tả mục tiêu, chưa triển khai
```
Chưa có thư mục seed cho `catalog`, `commerce`, `log`, `fulfillment`, `notification`, `wallet`. Riêng `catalog` có `Seeds/products/` (chỉ `README.md`, không phải nguồn dữ liệu).
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
---

## 5. GIỚI HẠN ĐÃ KIỂM CHỨNG CỦA MOCK ENGINE (2026-10-25)

Ba điểm dưới đây là hành vi thật của `Dispatching/MockDataMapper.cs` + `Dispatching/GatewayDispatcher.cs`, không phải đặc tả mục tiêu. Ai viết Controller Action mới phải biết trước khi hứa hẹn mã lỗi hay shape response.

### 5.1 Không mô phỏng được lỗi theo từng resource
`MockDataMapper.ResolveSeedFilePath(service, operation)` chỉ chọn file theo cặp `(service, operation)` — nó không đọc path parameter, query hay body. Vì vậy mock **không thể** trả `404`/`409`/`422` cho một `auctionId`/`productId` cụ thể.

Những mã lỗi Mock thật sự tạo được:
| Nguồn | Mã | Bằng chứng |
|:---|:---|:---|
| Validate trong Controller (`Common/*Validation.cs`) | `400 VAL_INVALID_INPUT` | `Common/AuctionValidation.cs` |
| Auth/Policy | `401 UNAUTHORIZED`, `403 FORBIDDEN` | `Security/SecurityConfiguration.cs` |
| Rate limit | `429 RATE_LIMITED` | `Middleware/RateLimiting*` |
| `MockStatus` của service | mọi mã cấu hình, `errorCode = MOCK_FAILURE` | `MockDataMapper.cs:51-61` |
| Seed thiếu file | `500 SEED_DATA_NOT_FOUND` | `MockDataMapper.cs:64-74` |

⇒ Muốn kiểm thử mã lỗi nghiệp vụ (`AUCTION_NOT_FOUND`, `PRODUCT_ALREADY_IN_AUCTION`, `INVALID_SCHEDULE`…) phải chuyển service sang `Mode: Real`; đừng ghi các mã đó vào test Mock.

### 5.2 Seed sai shape chỉ bị phát hiện ở nhánh phân trang
- `DispatchPagedAsync` gọi `parser.ParseJson(jsonResult)` **không** có try/catch (`GatewayDispatcher.cs:183`) ⇒ seed sai ⇒ `500`.
- `DispatchAsync` (object đơn) đi qua `MapMockAsync`, `catch {}` rỗng rồi trả JSON thô (`MockDataMapper.cs:87-92`) ⇒ seed sai shape vẫn `200`, chỉ là body lệch hợp đồng.

⇒ Mọi bộ seed mới phải kèm test khoá shape. Hiện có `Nexus.Gateway.Tests/AuctionSeedConformanceTests.cs` phủ 16/16 operation của auction.

### 5.3 Envelope Gateway không trùng 1-1 với hợp đồng API
Hợp đồng F2/F3 mô tả body phẳng; Gateway bọc trong `ApiResponse<T>` (Nguyên tắc 4.1 + `Responses/ApiResponse.cs`). Mapping đã chốt:

| Hợp đồng | Vị trí thật trong response Gateway |
|:---|:---|
| `items` | `data.items` |
| `page` | `data.pagination.pageIndex` |
| `pageSize` | `data.pagination.pageSize` |
| `total` | `data.pagination.totalCount` |
| `page`/`pageSize` **ghi trong file seed** | ❌ bị bỏ qua — `DispatchPagedAsync` lấy phân trang từ query string |

Ngoài ra `DispatchPagedAsync` chỉ đọc `items` + `total` của message, mọi field anh em khác trong cùng message bị rơi. Endpoint nào có field anh em (`rating`, `children`…) thì dùng `DispatchAsync` với message gói `items`, không dùng `DispatchPagedAsync`.
