# 04 — MOCK ENGINE & SEED DATA SPECIFICATION
> **Trạng thái triển khai (2026-10-25):** ⚠️ Một phần — 107/107 file seed thật phục vụ `auth` (4), `users` (60: 5 cơ bản + 55 luồng user/seller/admin), `catalog` (27), `auction` (16); 5/5 file declarative mock phục vụ 5 route; 2 thư mục seed nghiệp vụ còn lại mới có `README.md` (`cart`, `orders`).
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-25.

---

## 1. HAI CƠ CHẾ MOCK SONG SONG (ĐỀU ĐANG CHẠY)

| Cơ chế | Cách hoạt động | Có dữ liệu thật? | Trạng thái | Bằng chứng |
|:---|:---|:---|:---|:---|
| **Fixture theo Controller Action** | Controller gọi `_dispatcher.DispatchAsync(...)`; `MockDataMapper` đọc `Seeds/{service}/{operation}.json` rồi parse bằng `MessageParser<T>.ParseJson()` | 107 file (`auth` 4, `users` 60, `catalog` 27, `auction` 16) | ✅ CÓ | `Dispatching/MockDataMapper.cs:27`; `Nexus.Gateway/Seeds/` |
| **Declarative mock routes** | `MockEngineMiddleware` khớp `Mocks/*.mock.json` **khi không có endpoint `ControllerActionDescriptor` nào khớp** và effective mode của service là `Mock` | 5 file | ✅ CÓ | `MockEngine/MockEngineMiddleware.cs:30-51`; `Nexus.Gateway/Mocks/` |

---

## 2. CẤU TRÚC SEED FIXTURE THẬT ĐANG CÓ

```
Nexus.Gateway/Seeds/
├── README.md
├── auth/           ✅ 4 file: login.json, register.json, verify-otp.json, resend-otp.json
├── users/          ✅ 60 file: 5 file cơ bản + 55 file luồng user, profile, seller, review, report, admin
├── catalog/        ✅ 27 file: products, search, product, pics, rating, status, variants, categories, inventory, auction-products
├── auction/        ✅ 16 file: auctions, auction, bids, me-bids, result, create, update, cancel, recreate, seller, admin
├── cart/           ❌ chỉ có README.md — đặc tả mục tiêu, chưa triển khai
└── orders/         ❌ chỉ có README.md — đặc tả mục tiêu, chưa triển khai
```

Toàn bộ 107 file seed đều có Seed Conformance Tests tương ứng:
- `AuctionSeedConformanceTests.cs` (16 test cases)
- `CatalogSeedConformanceTests.cs` (27 test cases)
- `UserSeedConformanceTests.cs` (55 test cases)

---

## 3. DECLARATIVE MOCK DEFINITIONS (`Nexus.Gateway/Mocks/`)

5 file định nghĩa:

| File | Route | Service | Trạng thái | Bằng chứng |
|:---|:---|:---|:---|:---|
| `auth-login.mock.json` | `POST /api/v1/auth/login` | `users` | ✅ CÓ | `Mocks/auth-login.mock.json` |
| `auth-register.mock.json` | `POST /api/v1/auth/register` | `users` | ✅ CÓ | `Mocks/auth-register.mock.json` |
| `auth-verify-otp.mock.json` | `POST /api/v1/auth/verify-otp` | `users` | ✅ CÓ | `Mocks/auth-verify-otp.mock.json` |
| `auth-resend-otp.mock.json` | `POST /api/v1/auth/resend-otp` | `users` | ✅ CÓ | `Mocks/auth-resend-otp.mock.json` |
| `users-me.mock.json` | `GET /api/v1/users/me` | `users` | ✅ CÓ | `Mocks/users-me.mock.json` |

*Ghi chú: `catalog-products.mock.json` đã được xóa sau khi `ProductsController` tiếp nhận route `/api/v1/products`.*

---

## 4. NGUYÊN TẮC KHI THÊM DỮ LIỆU MOCK

1. Ưu tiên **Controller Action + Seed fixture** khi route cần envelope chuẩn (`success/statusCode/data/traceId/timestamp`) hoặc cần phân trang thật; dùng **declarative mock** khi chỉ cần trả body tĩnh cho route chưa có action.
2. Không viết state machine/in-memory DB trong Gateway: seed là dữ liệu tĩnh, mọi biến đổi nghiệp vụ thuộc backend.
3. Mọi file seed phải parse được bằng Protobuf JSON parser tương ứng; không thêm trường không có trong `.proto`.
4. Không sửa `Nexus.Gateway/Contracts/shop.proto` để "vừa khớp" seed — hợp đồng là nguồn sự thật.

---

## 5. GIỚI HẠN ĐÃ KIỂM CHỨNG CỦA MOCK ENGINE (2026-10-25)

### 5.1 Không mô phỏng được lỗi theo từng resource
`MockDataMapper.ResolveSeedFilePath(service, operation)` chỉ chọn file theo cặp `(service, operation)` — nó không đọc path parameter, query hay body. Vì vậy mock **không thể** trả `404`/`409`/`422` cho một `auctionId`/`productId` cụ thể.

Những mã lỗi Mock thật sự tạo được:
| Nguồn | Mã | Bằng chứng |
|:---|:---|:---|
| Validate trong Controller (`Common/*Validation.cs`) | `400 VAL_INVALID_INPUT` | `Common/AuctionValidation.cs`, `Common/CatalogValidation.cs`, `Common/UserValidation.cs` |
| Auth/Policy | `401 UNAUTHORIZED`, `403 FORBIDDEN` | `Security/SecurityConfiguration.cs` |
| Rate limit | `429 RATE_LIMITED` | `Middleware/RateLimiting*` |
| `MockStatus` của service | mọi mã cấu hình, `errorCode = MOCK_FAILURE` | `MockDataMapper.cs:51-61` |
| Seed thiếu file | `500 SEED_DATA_NOT_FOUND` | `MockDataMapper.cs:64-74` |

### 5.2 Seed sai shape chỉ bị phát hiện ở nhánh phân trang
- `DispatchPagedAsync` gọi `parser.ParseJson(jsonResult)` **không** có try/catch (`GatewayDispatcher.cs:183`) $\rightarrow$ seed sai $\rightarrow$ `500`.
- `DispatchAsync` (object đơn) đi qua `MapMockAsync`, `catch {}` rỗng rồi trả JSON thô (`MockDataMapper.cs:87-92`) $\rightarrow$ seed sai shape vẫn `200`, chỉ là body lệch hợp đồng.

### 5.3 Envelope Gateway không trùng 1-1 với hợp đồng API
Hợp đồng F2/F3 mô tả body phẳng; Gateway bọc trong `ApiResponse<T>` (Nguyên tắc 4.1 + `Responses/ApiResponse.cs`). Mapping đã chốt:

| Hợp đồng | Vị trí thật trong response Gateway |
|:---|:---|
| `items` | `data.items` |
| `page` | `data.pagination.pageIndex` |
| `pageSize` | `data.pagination.pageSize` |
| `total` | `data.pagination.totalCount` |
| `page`/`pageSize` **ghi trong file seed** | ❌ bị bỏ qua — `DispatchPagedAsync` lấy phân trang từ query string |

Ngoài ra `DispatchPagedAsync` chỉ đọc `items` + `total` của message. Endpoint nào có field anh em (`rating`, `children`…) thì dùng `DispatchAsync` với message gói `items`, không dùng `DispatchPagedAsync`.
