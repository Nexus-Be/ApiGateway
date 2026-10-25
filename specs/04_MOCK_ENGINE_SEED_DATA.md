# 04 — MOCK ENGINE & SEED DATA SPECIFICATION
> **Trạng thái triển khai (2026-10-25):** ⚠️ Một phần — 91/91 file seed thật phục vụ `auth` (4), `users` (60: 5 cơ bản + 55 luồng user/seller/admin), `catalog` (27); 5/5 file declarative mock phục vụ 5 route; 2 thư mục seed nghiệp vụ còn lại mới có `README.md` (`cart`, `orders`).
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-25.

---

## 1. HAI CƠ CHẾ MOCK SONG SONG (ĐỀU ĐANG CHẠY)

| Cơ chế | Cách hoạt động | Có dữ liệu thật? | Trạng thái | Bằng chứng |
|:---|:---|:---|:---|:---|
| **Fixture theo Controller Action** | Controller gọi `_dispatcher.DispatchAsync(...)` / `DispatchPagedAsync(...)` / `DispatchNoContentAsync(...)`; `MockDataMapper` đọc `Seeds/{service}/{operation}.json` rồi parse bằng `MessageParser<T>.ParseJson()` | 91 file (`auth` 4, `users` 60, `catalog` 27) | ✅ CÓ | `Dispatching/MockDataMapper.cs:27`; `Nexus.Gateway/Seeds/` |
| **Declarative mock routes** | `MockEngineMiddleware` khớp `Mocks/*.mock.json` **khi không có endpoint `ControllerActionDescriptor` nào khớp** và effective mode của service là `Mock` | 5 file | ✅ CÓ | `MockEngine/MockEngineMiddleware.cs:30-51`; `Nexus.Gateway/Mocks/` |

Tài liệu cũ khẳng định "declarative `*.mock.json` không được dùng, chỉ Controller + Seeds mới hợp lệ" là **sai**: cả hai cơ chế đều đang chạy. Declarative mock chỉ phục vụ route **chưa có controller action**; ngay khi service chuyển `Real`, route không có controller tương ứng trả **404** (không tự forward).

---

## 2. CẤU TRÚC SEED FIXTURE THẬT ĐANG CÓ

```
Nexus.Gateway/Seeds/
├── README.md
├── auth/           ✅ 4 file: login.json, register.json, verify-otp.json, resend-otp.json
├── users/          ✅ 60 file: 5 file cơ bản + 55 file nghiệp vụ User/Seller/Admin Flow
├── catalog/        ✅ 27 file: khớp 27 operation của Catalog Flow (F2)
├── cart/           ❌ chỉ có README.md — đặc tả mục tiêu, chưa triển khai
└── orders/         ❌ chỉ có README.md — đặc tả mục tiêu, chưa triển khai
```

### 2.1 Quy ước đặt tên file seed
- Đường dẫn: `Seeds/{service}/{operation}.json`, trong đó `{service}` là tên service trong `Gateway:Services` (`users`, `auth`, `catalog`, `commerce`, `log`, `auction`, `fulfillment`, `notification`, `wallet`) và `{operation}` là tên thao tác lowercase, có gạch nối (`products`, `product-search`, `variant-create`...).
- Nội dung file là **JSON của message Protobuf tương ứng**, phải parse được bằng `MessageParser<T>.ParseJson()` — sai kiểu/thiếu trường bắt buộc sẽ lỗi ngay khi request.
- Không lưu dữ liệu động/state trong seed: seed là dữ liệu tĩnh. Toàn bộ copy trong file seed là tiếng Anh.

### 2.2 Danh mục seed Catalog (27 files)
| File Seed | Phục vụ Route | Message Protobuf Tương Ứng |
|:---|:---|:---|
| `Seeds/catalog/products.json` | `GET /api/v1/products` | `ProductListResponse` |
| `Seeds/catalog/product-search.json` | `GET /api/v1/product/search` | `ProductListResponse` |
| `Seeds/catalog/product.json` | `GET /api/v1/product/{productId}` | `ProductDetailResponse` |
| `Seeds/catalog/product-create.json` | `POST /api/v1/products` | `ProductCreatedResponse` |
| `Seeds/catalog/product-update.json` | `PATCH /api/v1/product/{productId}` | `ProductUpdatedResponse` |
| `Seeds/catalog/product-delete.json` | `DELETE /api/v1/product/{productId}` | `DeleteResponse` |
| `Seeds/catalog/product-images-add.json` | `POST /api/v1/product/{productId}pics` | `ProductImageAddResponse` |
| `Seeds/catalog/product-images.json` | `GET /api/v1/product/{productId}pics` | `ProductImageListResponse` |
| `Seeds/catalog/product-images-delete.json` | `DELETE /api/v1/product/{productId}pics` | `DeleteResponse` |
| `Seeds/catalog/product-rating-total.json` | `GET /api/v1/products/{productId}/totalRating` | `ProductRatingSummaryResponse` |
| `Seeds/catalog/product-rating.json` | `GET /api/v1/products/{productId}/rating` | `ProductRatingResponse` |
| `Seeds/catalog/product-status.json` | `PATCH /api/v1/product/{productId}/status` | `ProductStatusResponse` |
| `Seeds/catalog/variant-create.json` | `POST /api/v1/product/{productId}/variants` | `VariantCreatedResponse` |
| `Seeds/catalog/variant-delete.json` | `DELETE /api/v1/variant/{variantId}` | `DeleteResponse` |
| `Seeds/catalog/variant-update.json` | `PATCH /api/v1/variant/{variantId}` | `VariantUpdatedResponse` |
| `Seeds/catalog/variant.json` | `GET /api/v1/variant/{variantId}` | `VariantDetailResponse` |
| `Seeds/catalog/auction-product-create.json` | `POST /api/v1/product/auction` | `AuctionProductCreatedResponse` |
| `Seeds/catalog/auction-product-search.json` | `GET /api/v1/product/auction/search` | `ProductListResponse` |
| `Seeds/catalog/auction-products.json` | `GET /api/v1/product/auctions` | `ProductListResponse` |
| `Seeds/catalog/auction-product.json` | `GET /api/v1/product/auction/{auctionId}` | `AuctionProductDetailResponse` |
| `Seeds/catalog/categories.json` | `GET /api/v1/categories` | `CategoryListResponse` |
| `Seeds/catalog/category.json` | `GET /api/v1/categories/{categoryId}` | `CategoryDetailResponse` |
| `Seeds/catalog/category-create.json` | `POST /api/v1/categories` | `CategoryCreatedResponse` |
| `Seeds/catalog/category-update.json` | `PUT /api/v1/categories/{categoryId}` | `CategoryUpdatedResponse` |
| `Seeds/catalog/category-delete.json` | `DELETE /api/v1/categories/{categoryId}` | `DeleteResponse` |
| `Seeds/catalog/inventory-stock-in.json` | `POST /api/v1/inventory/stock-in` | `StockInResponse` |
| `Seeds/catalog/variant-stock-in.json` | `POST /api/v1/variant/{variantId}/stock-in` | `StockInVariantResponse` |

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

Ba điểm dưới đây là hành vi thật của `Dispatching/MockDataMapper.cs` + `Dispatching/GatewayDispatcher.cs`:

### 5.1 Không mô phỏng được lỗi theo từng resource
`MockDataMapper.ResolveSeedFilePath(service, operation)` chỉ chọn file theo cặp `(service, operation)` — không đọc path parameter, query hay body. Vì vậy mock **không thể** trả `404`/`409`/`422` cho một `productId`/`categoryId` cụ thể.

Những mã lỗi Mock thật sự tạo được:
| Nguồn | Mã | Bằng chứng |
|:---|:---|:---|
| Validate trong Controller (`Common/*Validation.cs`) | `400 VAL_INVALID_INPUT` | `Common/CatalogValidation.cs` |
| Auth/Policy | `401 UNAUTHORIZED`, `403 FORBIDDEN` | `Security/SecurityConfiguration.cs` |
| Rate limit | `429 RATE_LIMITED` | `Middleware/RateLimiting*` |
| `MockStatus` của service | mọi mã cấu hình, `errorCode = MOCK_FAILURE` | `MockDataMapper.cs:51-61` |
| Seed thiếu file | `500 SEED_DATA_NOT_FOUND` | `MockDataMapper.cs:64-74` |

⇒ Muốn kiểm thử mã lỗi nghiệp vụ (`PRODUCT_NOT_FOUND`, `CATEGORY_INVALID`, `SKU_CODE_DUPLICATED`…) phải chuyển service sang `Mode: Real`; đừng ghi các mã đó vào test Mock.

### 5.2 Seed sai shape chỉ bị phát hiện ở nhánh phân trang
- `DispatchPagedAsync` gọi `parser.ParseJson(jsonResult)` **không** có try/catch (`GatewayDispatcher.cs:183`) ⇒ seed sai ⇒ `500`.
- `DispatchAsync` (object đơn) đi qua `MapMockAsync`, `catch {}` rỗng rồi trả JSON thô (`MockDataMapper.cs:87-92`) ⇒ seed sai shape vẫn `200`, chỉ là body lệch hợp đồng.

⇒ Mọi bộ seed mới phải kèm test khóa shape: `Nexus.Gateway.Tests/CatalogSeedConformanceTests.cs` (27 operation) và `Nexus.Gateway.Tests/UserSeedConformanceTests.cs` (55 operation) kiểm chứng toàn bộ fixture theo message protobuf.

### 5.3 Envelope Gateway không trùng 1-1 với hợp đồng API
Hợp đồng F2 mô tả body phẳng; Gateway bọc trong `ApiResponse<T>` (Nguyên tắc 4.1 + `Responses/ApiResponse.cs`). Mapping đã chốt:

| Hợp đồng F2 | Vị trí thật trong response Gateway |
|:---|:---|
| `items` | `data.items` |
| `page` | `data.pagination.pageIndex` |
| `pageSize` | `data.pagination.pageSize` |
| `total` | `data.pagination.totalCount` |
| `page`/`pageSize` **ghi trong file seed** | ❌ bị bỏ qua — `DispatchPagedAsync` lấy phân trang từ query string |

Ngoài ra `DispatchPagedAsync` chỉ đọc `items` + `total` của message. Endpoint nào có field anh em (`rating`, `children`…) thì dùng `DispatchAsync` với message gói `items`, không dùng `DispatchPagedAsync`.
