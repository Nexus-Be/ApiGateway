# 04 — SEED DATA SPECIFICATION
> **Trạng thái triển khai (2026-10-30):** ✅ ĐÃ CHUẨN HÓA — **một cơ chế mock duy nhất** (Controller Action + Seed fixture). Declarative mock (`Mocks/*.mock.json`) và `MockEngine/` đã bị gỡ bỏ hoàn toàn. **163 file seed** phục vụ 9 nhóm service.
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-30.

---

## 1. CƠ CHẾ MOCK DUY NHẤT

| Cơ chế | Cách hoạt động | Có dữ liệu thật? | Trạng thái | Bằng chứng |
|:---|:---|:---|:---|:---|
| **Fixture theo Controller Action** | Controller gọi `ForwardAsync` / `ForwardPagedAsync`; `MockDataMapper` đọc `Seeds/{service}/{operation}.json` rồi trả `JsonElement` | 163 file | ✅ CÓ | `Dispatching/MockDataMapper.cs`; `Nexus.Gateway/Seeds/` |

Không còn middleware nào phục vụ route không có controller action. Route không khớp Controller Action trả **404**.

---

## 2. CẤU TRÚC SEED FIXTURE THẬT ĐANG CÓ

```
Nexus.Gateway/Seeds/
├── README.md
├── auth/           ✅ 4 file: login, register, verify-otp, resend-otp
├── users/          ✅ 60 file: profile, auth, seller, review, report, admin flows
├── catalog/        ✅ 28 file: products, search, product, pics, rating, status, variants, categories, inventory, auction-products
├── commerce/       ✅ 20 file: cart, orders, sub-orders, checkout, payment
├── auction/        ✅ 18 file: auctions, bids, result, create, update, cancel, recreate, seller, admin
├── notification/   ✅ 13 file: devices, preferences, notifications, templates, channels, event-logs
├── fulfillment/    ✅ 8 file: warehouses, inventory, shipping quote, shipments
├── wallet/         ✅ 7 file: balance, topup, withdraw, transfer, transactions, admin
├── log/            ✅ 5 file: logs, log, event-types, event-type-update, batch
└── (không còn thư mục placeholder)
```

Toàn bộ file seed đều có Seed Conformance Tests tương ứng:
- `AuctionSeedConformanceTests.cs`
- `CatalogSeedConformanceTests.cs`
- `UserSeedConformanceTests.cs`

Mọi `mockSeed` khai báo trong Controller đều phải có file tương ứng. `MockDataMapper` trả `SEED_DATA_NOT_FOUND` (HTTP 500) khi thiếu fixture.

---

## 3. NGUYÊN TẮC KHI THÊM DỮ LIỆU MOCK

1. Luôn dùng **Controller Action + Seed fixture**: route cần envelope chuẩn (`success/statusCode/data/traceId/timestamp`), phân trang thật, DTO binding, `[Authorize]` và rate limit.
2. Không viết state machine/in-memory DB trong Gateway: seed là dữ liệu tĩnh, mọi biến đổi nghiệp vụ thuộc backend.
3. File seed phải khớp shape response thật của backend service tương ứng (JSON thuần, camelCase).
4. Tên operation trong `mockSeed` là nguồn sự thật cho tên file: `Seeds/{service}/{mockSeed}.json`.

---

## 4. GIỚI HẠN ĐÃ KIỂM CHỨNG CỦA MOCK DISPATCHER (2026-10-30)

### 4.1 Không mô phỏng được lỗi theo từng resource
`MockDataMapper.ResolveSeedFilePath(service, operation)` chỉ chọn file theo cặp `(service, operation)` — nó không đọc path parameter, query hay body. Vì vậy mock **không thể** trả `404`/`409`/`422` cho một `auctionId`/`productId` cụ thể.

Những mã lỗi Mock thật sự tạo được:
| Nguồn | Mã | Bằng chứng |
|:---|:---|:---|
| Validate trong Controller (`Common/*Validation.cs`) | `400 VAL_INVALID_INPUT` | `Common/AuctionValidation.cs`, `Common/CatalogValidation.cs`, `Common/UserValidation.cs` |
| Auth/Policy | `401 AUTH_UNAUTHORIZED`, `403 AUTH_FORBIDDEN` | `Security/SecurityConfiguration.cs` |
| Rate limit | `429 RATE_LIMIT_EXCEEDED` | `Middleware/RedisRateLimitMiddleware.cs` |
| `MockStatus` của service | mọi mã cấu hình, `errorCode = MOCK_FAILURE` | `Dispatching/MockDataMapper.cs` |
| Seed thiếu file | `500 SEED_DATA_NOT_FOUND` | `Dispatching/MockDataMapper.cs` |

### 4.2 Seed sai shape chỉ bị phát hiện ở nhánh phân trang
- `DispatchRestPagedAsync` gọi `JsonDocument.Parse(jsonResult)` **không** có try/catch → seed sai → `500`.
- `DispatchRestAsync` (object đơn) trả JSON thô trong `data` → seed sai shape vẫn `200`, chỉ là body lệch hợp đồng.

### 4.3 Envelope Gateway không trùng 1-1 với hợp đồng API
Hợp đồng backend mô tả body phẳng; Gateway bọc trong `ApiResponse<T>` (Nguyên tắc 4.1 + `Responses/ApiResponse.cs`). Mapping đã chốt:

| Hợp đồng | Vị trí thật trong response Gateway |
|:---|:---|
| `items` | `data.items` |
| `page` | `data.pagination.pageIndex` |
| `pageSize` | `data.pagination.pageSize` |
| `total` | `data.pagination.totalCount` |
| `page`/`pageSize` **ghi trong file seed** | ❌ bị bỏ qua — `DispatchRestPagedAsync` lấy phân trang từ query string |

Ngoài ra `DispatchRestPagedAsync` chỉ đọc `items` + `total` của JSON. Endpoint nào có field anh em (`rating`, `children`…) thì dùng `DispatchRestAsync` với object gói `items`, không dùng `DispatchRestPagedAsync`.
